using MediatR;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.JobOffers.Commands;

public class AcceptJobOfferCommandHandler
    : IRequestHandler<AcceptJobOfferCommand, AcceptJobOfferResult>
{
    private readonly IJobOfferRepository _jobOfferRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IRepStateRepository _repStateRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRequesterHubService _requesterHub;
    private readonly IDispatchHubService _dispatchHub;

    public AcceptJobOfferCommandHandler(
        IJobOfferRepository jobOfferRepository,
        IServiceRequestRepository serviceRequestRepository,
        IRepStateRepository repStateRepository,
        IVehicleRepository vehicleRepository,
        IUserRepository userRepository,
        IRequesterHubService requesterHub,
        IDispatchHubService dispatchHub)
    {
        _jobOfferRepository = jobOfferRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _repStateRepository = repStateRepository;
        _vehicleRepository = vehicleRepository;
        _userRepository = userRepository;
        _requesterHub = requesterHub;
        _dispatchHub = dispatchHub;
    }

    public async Task<AcceptJobOfferResult> Handle(AcceptJobOfferCommand request, CancellationToken cancellationToken)
    {
        var offer = await _jobOfferRepository.GetByIdAsync(request.OfferId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job offer {request.OfferId} was not found.");

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(offer.ServiceRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request {offer.ServiceRequestId} was not found.");

        var repState = await _repStateRepository.GetByRepIdAsync(offer.RepId, cancellationToken)
            ?? throw new KeyNotFoundException($"Rep state for rep {offer.RepId} was not found.");

        var oldRepState = repState.State.ToString();

        offer.Accept();
        serviceRequest.AssignTo(offer.RepId);
        repState.GoEnRoute(serviceRequest.Id);

        await _jobOfferRepository.UpdateAsync(offer, cancellationToken);
        await _serviceRequestRepository.UpdateAsync(serviceRequest, cancellationToken);
        await _repStateRepository.UpsertAsync(repState, cancellationToken);

        var etaMinutes = await BroadcastAsync(offer, serviceRequest, oldRepState, repState.State.ToString(), cancellationToken);

        await EmitDeferredRepRedirectedAsync(offer, serviceRequest, etaMinutes, cancellationToken);

        return new AcceptJobOfferResult(
            offer.Id,
            serviceRequest.Id,
            offer.Status.ToString(),
            serviceRequest.Status.ToString(),
            repState.State.ToString());
    }

    // The displaced requester's RepRedirected notification is deferred until a NEW rep accepts the
    // request that was returned to Pending by a redirect (stamped via ServiceRequest.DisplacedFromRepId).
    // This is additive to the accept flow (Open/Closed) and is a one-shot: the stamp is cleared and
    // persisted so the event never fires twice.
    private async Task EmitDeferredRepRedirectedAsync(
        JobOffer offer,
        ServiceRequest serviceRequest,
        double newEtaMinutes,
        CancellationToken cancellationToken)
    {
        if (serviceRequest.DisplacedFromRepId is not Guid displacedRepId)
            return;

        var oldRep = await _userRepository.FindByIdAsync(displacedRepId, cancellationToken);
        var newRep = await _userRepository.FindByIdAsync(offer.RepId, cancellationToken);

        await _requesterHub.SendRepRedirectedAsync(
            $"requester:{serviceRequest.RequesterId}",
            new RepRedirectedPayload(oldRep?.Name ?? string.Empty, newRep?.Name ?? string.Empty, newEtaMinutes),
            cancellationToken);

        serviceRequest.ClearDisplacement();
        await _serviceRequestRepository.UpdateAsync(serviceRequest, cancellationToken);
    }

    private async Task<double> BroadcastAsync(
        JobOffer offer,
        ServiceRequest serviceRequest,
        string oldRepState,
        string newRepState,
        CancellationToken cancellationToken)
    {
        var rep = await _userRepository.FindByIdAsync(offer.RepId, cancellationToken);
        var repName = rep?.Name ?? string.Empty;

        var (etaMinutes, latitude, longitude, vehicleRegistration) =
            await ComputeEtaAndPositionAsync(offer.RepId, serviceRequest, cancellationToken);

        await _requesterHub.SendRepAssignedAsync(
            $"requester:{serviceRequest.RequesterId}",
            new RepAssignedPayload(offer.RepId, repName, etaMinutes, latitude, longitude, vehicleRegistration),
            cancellationToken);

        await _dispatchHub.SendServiceRequestAssignedAsync(
            $"dealer:{serviceRequest.DealerId}",
            new ServiceRequestAssignedPayload(serviceRequest.Id, offer.RepId, repName, etaMinutes),
            cancellationToken);

        await _dispatchHub.SendRepStateChangedAsync(
            $"dealer:{serviceRequest.DealerId}",
            new RepStateChangedPayload(offer.RepId, oldRepState, newRepState),
            cancellationToken);

        return etaMinutes;
    }

    private async Task<(double EtaMinutes, double Latitude, double Longitude, string VehicleRegistration)> ComputeEtaAndPositionAsync(
        Guid repId,
        ServiceRequest serviceRequest,
        CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByClaimedRepIdAsync(repId, cancellationToken);
        var registration = vehicle?.Registration ?? string.Empty;

        if (vehicle is not { LastLatitude: not null, LastLongitude: not null })
            return (0, 0, 0, registration);

        var distanceMiles = HaversineCalculator.DistanceMiles(
            vehicle.LastLatitude.Value, vehicle.LastLongitude.Value,
            serviceRequest.Latitude, serviceRequest.Longitude);

        return (HaversineCalculator.EtaMinutes(distanceMiles), vehicle.LastLatitude.Value, vehicle.LastLongitude.Value, registration);
    }
}
