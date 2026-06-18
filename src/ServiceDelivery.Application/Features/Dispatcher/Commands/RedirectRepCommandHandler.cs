using MediatR;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Dispatcher.Commands;

public class RedirectRepCommandHandler
    : IRequestHandler<RedirectRepCommand, RedirectRepResult>
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IRepStateRepository _repStateRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDiagnosticTroubleCodeRepository _dtcRepository;
    private readonly IMatchingService _matchingService;
    private readonly IRepHubService _repHub;
    private readonly IRequesterHubService _requesterHub;
    private readonly IDispatchHubService _dispatchHub;
    private readonly RedirectOptions _options;
    private readonly Func<DateTime> _now;

    private const double ProximityGuardMiles = 15.0;

    public RedirectRepCommandHandler(
        IServiceRequestRepository serviceRequestRepository,
        IRepStateRepository repStateRepository,
        IVehicleRepository vehicleRepository,
        IUserRepository userRepository,
        IDiagnosticTroubleCodeRepository dtcRepository,
        IMatchingService matchingService,
        IRepHubService repHub,
        IRequesterHubService requesterHub,
        IDispatchHubService dispatchHub,
        RedirectOptions options,
        Func<DateTime>? now = null)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _repStateRepository = repStateRepository;
        _vehicleRepository = vehicleRepository;
        _userRepository = userRepository;
        _dtcRepository = dtcRepository;
        _matchingService = matchingService;
        _repHub = repHub;
        _requesterHub = requesterHub;
        _dispatchHub = dispatchHub;
        _options = options;
        _now = now ?? (() => DateTime.UtcNow);
    }

    public async Task<RedirectRepResult> Handle(RedirectRepCommand request, CancellationToken cancellationToken)
    {
        var newRequest = await _serviceRequestRepository.GetByIdAsync(request.ToRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service request {request.ToRequestId} was not found.");

        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken)
            ?? throw new KeyNotFoundException($"Rep state for rep {request.RepId} was not found.");

        var displacedRequest = await _serviceRequestRepository.GetActiveByRepIdAsync(request.RepId, cancellationToken);
        var vehicle = await _vehicleRepository.GetByClaimedRepIdAsync(request.RepId, cancellationToken);

        EnsureEligible(repState, displacedRequest, newRequest, vehicle);

        var displaced = displacedRequest!;

        displaced.ReturnToPendingDisplacedBy(request.RepId);
        repState.Redirect(newRequest.Id, _now());

        await _serviceRequestRepository.UpdateAsync(displaced, cancellationToken);
        await _repStateRepository.UpsertAsync(repState, cancellationToken);

        await _matchingService.RunAsync(displaced.Id, cancellationToken);

        await BroadcastAsync(request, newRequest, vehicle, cancellationToken);

        return new RedirectRepResult(
            request.RepId,
            displaced.Id,
            newRequest.Id,
            repState.State.ToString());
    }

    private void EnsureEligible(
        RepStateRecord repState,
        ServiceRequest? displacedRequest,
        ServiceRequest newRequest,
        Vehicle? vehicle)
    {
        if (displacedRequest is null)
            throw new RedirectNotAllowedException("RepNotEnRoute",
                $"Rep {repState.RepId} has no active request and cannot be redirected.");

        if (repState.State == RepState.OnSite)
            throw new RedirectNotAllowedException("RepOnSite",
                $"Rep {repState.RepId} is on site and cannot be redirected.");

        if (repState.State != RepState.EnRoute)
            throw new RedirectNotAllowedException("RepNotEnRoute",
                $"Rep {repState.RepId} is {repState.State} and cannot be redirected; only an EnRoute rep can be redirected.");

        if (IsWithinProximityGuard(vehicle, displacedRequest))
            throw new RedirectNotAllowedException("WithinFifteenMiles",
                $"Rep {repState.RepId} is within {ProximityGuardMiles} miles of the current requester and cannot be redirected.");

        var newIsGold = newRequest.Tier == ServiceTier.Gold;

        if (!newIsGold && newRequest.Tier <= displacedRequest.Tier)
            throw new RedirectNotAllowedException("TierNotHigher",
                "The target request tier must be strictly higher than the current job, or Gold.");

        if (!newIsGold && IsCooldownActive(repState))
            throw new RedirectNotAllowedException("CooldownActive",
                $"Rep {repState.RepId} is within the {_options.CooldownMinutes}-minute redirect cooldown.");
    }

    private static bool IsWithinProximityGuard(Vehicle? vehicle, ServiceRequest displacedRequest)
    {
        if (vehicle is not { LastLatitude: not null, LastLongitude: not null })
            return false;

        var distanceMiles = HaversineCalculator.DistanceMiles(
            vehicle.LastLatitude.Value, vehicle.LastLongitude.Value,
            displacedRequest.Latitude, displacedRequest.Longitude);

        return distanceMiles < ProximityGuardMiles;
    }

    private bool IsCooldownActive(RepStateRecord repState)
    {
        if (repState.LastRedirectedAt is null)
            return false;

        var elapsed = _now() - repState.LastRedirectedAt.Value;
        return elapsed < TimeSpan.FromMinutes(_options.CooldownMinutes);
    }

    private async Task BroadcastAsync(
        RedirectRepCommand request,
        ServiceRequest newRequest,
        Vehicle? vehicle,
        CancellationToken cancellationToken)
    {
        var (distanceMiles, etaMinutes, latitude, longitude) = ComputeProximity(vehicle, newRequest);

        var newRequester = await _userRepository.FindByIdAsync(newRequest.RequesterId, cancellationToken);
        var dtc = await _dtcRepository.GetByIdAsync(newRequest.DtcId, cancellationToken);
        var rep = await _userRepository.FindByIdAsync(request.RepId, cancellationToken);

        await _repHub.SendRedirectReceivedAsync(
            $"rep:{request.RepId}",
            new RedirectReceivedPayload(
                newRequest.Id,
                newRequester?.Name ?? string.Empty,
                newRequest.Tier.ToString(),
                dtc?.HumanReadableTitle ?? string.Empty,
                newRequest.Latitude,
                newRequest.Longitude,
                distanceMiles,
                etaMinutes),
            cancellationToken);

        await _requesterHub.SendRepAssignedAsync(
            $"requester:{newRequest.RequesterId}",
            new RepAssignedPayload(request.RepId, rep?.Name ?? string.Empty, etaMinutes, latitude, longitude),
            cancellationToken);

        var stateName = RepState.EnRoute.ToString();
        await _dispatchHub.SendRepStateChangedAsync(
            $"dealer:{request.DealerId}",
            new RepStateChangedPayload(request.RepId, stateName, stateName),
            cancellationToken);
    }

    private static (double DistanceMiles, double EtaMinutes, double Latitude, double Longitude) ComputeProximity(
        Vehicle? vehicle,
        ServiceRequest target)
    {
        if (vehicle is not { LastLatitude: not null, LastLongitude: not null })
            return (0, 0, 0, 0);

        var distanceMiles = HaversineCalculator.DistanceMiles(
            vehicle.LastLatitude.Value, vehicle.LastLongitude.Value,
            target.Latitude, target.Longitude);

        return (distanceMiles, HaversineCalculator.EtaMinutes(distanceMiles), vehicle.LastLatitude.Value, vehicle.LastLongitude.Value);
    }
}
