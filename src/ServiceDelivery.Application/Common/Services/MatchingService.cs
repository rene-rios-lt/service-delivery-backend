using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Common.Services;

public class MatchingService : IMatchingService
{
    private readonly IServiceRequestRepository _requests;
    private readonly IDiagnosticTroubleCodeRepository _dtcs;
    private readonly IRepStateRepository _repStates;
    private readonly IJobOfferRepository _jobOffers;
    private readonly IUserRepository _users;
    private readonly IRepHubService _repHub;
    private readonly IDispatchHubService _dispatchHub;
    private readonly int _offerExpirySeconds;

    public MatchingService(
        IServiceRequestRepository requests,
        IDiagnosticTroubleCodeRepository dtcs,
        IRepStateRepository repStates,
        IJobOfferRepository jobOffers,
        IUserRepository users,
        IRepHubService repHub,
        IDispatchHubService dispatchHub,
        MatchingOptions options)
    {
        _requests = requests;
        _dtcs = dtcs;
        _repStates = repStates;
        _jobOffers = jobOffers;
        _users = users;
        _repHub = repHub;
        _dispatchHub = dispatchHub;
        _offerExpirySeconds = options.OfferExpirySeconds;
    }

    public async Task RunAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetByIdAsync(requestId, cancellationToken);
        if (request is null)
            return;

        var dtc = await _dtcs.GetByIdAsync(request.DtcId, cancellationToken);
        if (dtc is null)
            return;

        var candidates = await _repStates.GetAvailableByDealerAsync(request.DealerId, cancellationToken);
        var skippedRepIds = await _jobOffers.GetSkippedRepIdsForRequestAsync(request.Id, cancellationToken);

        var winner = candidates
            .Where(c => c.Equipment.Contains(dtc.RequiredEquipmentType))
            .Where(c => !skippedRepIds.Contains(c.RepId))
            .OrderBy(c => HaversineCalculator.DistanceMiles(
                c.VehicleLatitude, c.VehicleLongitude, request.Latitude, request.Longitude))
            .ThenBy(c => c.AvailableSince)
            .FirstOrDefault();
        if (winner is null)
        {
            var pendingPayload = new ServiceRequestPendingPayload(
                request.Id,
                request.Tier.ToString(),
                dtc.HumanReadableTitle,
                $"{request.Latitude},{request.Longitude}");
            await _dispatchHub.SendServiceRequestPendingAsync($"dealer:{request.DealerId}", pendingPayload, cancellationToken);
            return;
        }

        // BUG-058: idempotency guard — if a live Pending offer already exists for this request,
        // skip creating a second. "Live" = Status == Pending AND ExpiresAt > now (both required).
        // An expired-but-unswept Pending offer (ExpiresAt <= now) is semantically dead and must
        // not block re-offering — that would re-introduce the BUG-054 skip-list starvation.
        var liveOffer = await _jobOffers.GetLivePendingOfferForRequestAsync(
            request.Id, DateTime.UtcNow, cancellationToken);
        if (liveOffer is not null)
            return;

        var now = DateTime.UtcNow;
        var offer = new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            RepId = winner.RepId,
            OfferedAt = now,
            ExpiresAt = now.AddSeconds(_offerExpirySeconds),
            Status = JobOfferStatus.Pending
        };
        await _jobOffers.AddAsync(offer, cancellationToken);

        var requester = await _users.FindByIdAsync(request.RequesterId, cancellationToken);
        var distanceMiles = HaversineCalculator.DistanceMiles(
            winner.VehicleLatitude, winner.VehicleLongitude, request.Latitude, request.Longitude);

        var payload = new JobOfferReceivedPayload(
            offer.Id,
            request.Id,
            requester?.Name ?? string.Empty,
            request.Tier.ToString(),
            dtc.HumanReadableTitle,
            request.Latitude,
            request.Longitude,
            distanceMiles,
            HaversineCalculator.EtaMinutes(distanceMiles));

        await _repHub.SendJobOfferReceivedAsync($"rep:{winner.RepId}", payload, cancellationToken);
    }

    public async Task RunForPendingByDealerAsync(Guid dealerId, CancellationToken cancellationToken = default)
    {
        var pending = await _requests.GetPendingByDealerAsync(dealerId, cancellationToken);
        foreach (var request in pending)
            await RunAsync(request.Id, cancellationToken);
    }
}
