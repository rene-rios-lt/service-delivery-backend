using MediatR;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public class TakeOverVehicleCommandHandler : IRequestHandler<TakeOverVehicleCommand, TakeOverVehicleResult>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRepSessionRepository _repSessionRepository;
    private readonly IRepStateRepository _repStateRepository;
    private readonly IDispatchHubService _dispatchHub;

    public TakeOverVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IRepSessionRepository repSessionRepository,
        IRepStateRepository repStateRepository,
        IDispatchHubService dispatchHub)
    {
        _vehicleRepository = vehicleRepository;
        _repSessionRepository = repSessionRepository;
        _repStateRepository = repStateRepository;
        _dispatchHub = dispatchHub;
    }

    public async Task<TakeOverVehicleResult> Handle(TakeOverVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");

        var callerState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken);
        if (!IsRepIdle(callerState))
            throw new RepNotIdleException(
                $"Rep {request.RepId} is not idle and cannot take over a vehicle.");

        var displacedRepId = vehicle.ClaimedByRepId;
        RepStateRecord? displacedState = null;
        if (displacedRepId is not null)
        {
            displacedState = await _repStateRepository.GetByRepIdAsync(displacedRepId.Value, cancellationToken);
            if (IsRepOnActiveJob(displacedState))
                throw new VehicleNotIdleException(
                    $"Vehicle {request.VehicleId} is not idle; its current rep has an active job.");
        }

        var now = DateTime.UtcNow;
        var callerOldState = (callerState?.State ?? RepState.Offline).ToString();

        await ReleaseDisplacedRepAsync(displacedRepId, displacedState, now, cancellationToken);
        await EndCallerPriorSessionAsync(request.RepId, now, cancellationToken);
        var newSession = await ClaimForCallerAsync(request, now, cancellationToken);
        var callerNewState = await SetCallerAvailableAsync(request.RepId, callerState, cancellationToken);

        vehicle.ClaimedByRepId = request.RepId;
        vehicle.ClaimedAt = now;
        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);

        await BroadcastTakeOverAsync(vehicle, request.RepId, callerOldState, callerNewState.State.ToString(), cancellationToken);

        return new TakeOverVehicleResult(
            newSession.Id,
            vehicle.Id,
            request.RepId,
            callerNewState.State.ToString(),
            newSession.StartedAt);
    }

    private async Task ReleaseDisplacedRepAsync(
        Guid? displacedRepId,
        RepStateRecord? displacedState,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (displacedRepId is null)
            return;

        var displacedSession = await _repSessionRepository.GetActiveByRepIdAsync(displacedRepId.Value, cancellationToken);
        if (displacedSession is not null)
        {
            displacedSession.EndedAt = now;
            await _repSessionRepository.UpdateAsync(displacedSession, cancellationToken);
        }

        if (displacedState is not null)
        {
            displacedState.GoOffline();
            await _repStateRepository.UpsertAsync(displacedState, cancellationToken);
        }
    }

    private async Task EndCallerPriorSessionAsync(Guid callerRepId, DateTime now, CancellationToken cancellationToken)
    {
        var priorSession = await _repSessionRepository.GetActiveByRepIdAsync(callerRepId, cancellationToken);
        if (priorSession is null)
            return;

        priorSession.EndedAt = now;
        await _repSessionRepository.UpdateAsync(priorSession, cancellationToken);
    }

    private async Task<RepSession> ClaimForCallerAsync(TakeOverVehicleCommand request, DateTime now, CancellationToken cancellationToken)
    {
        var session = new RepSession
        {
            Id = Guid.NewGuid(),
            RepId = request.RepId,
            VehicleId = request.VehicleId,
            StartedAt = now
        };
        await _repSessionRepository.AddAsync(session, cancellationToken);
        return session;
    }

    private async Task<RepStateRecord> SetCallerAvailableAsync(Guid callerRepId, RepStateRecord? callerState, CancellationToken cancellationToken)
    {
        var state = callerState ?? new RepStateRecord { RepId = callerRepId };
        state.TakeOver();
        await _repStateRepository.UpsertAsync(state, cancellationToken);
        return state;
    }

    private async Task BroadcastTakeOverAsync(
        Vehicle vehicle,
        Guid callerRepId,
        string callerOldState,
        string callerNewState,
        CancellationToken cancellationToken)
    {
        var dealerGroup = $"dealer:{vehicle.DealerId}";

        await _dispatchHub.SendRepStateChangedAsync(
            dealerGroup,
            new RepStateChangedPayload(callerRepId, callerOldState, callerNewState),
            cancellationToken);

        await _dispatchHub.SendFleetPositionUpdateAsync(
            dealerGroup,
            new FleetPositionUpdatePayload(
                callerRepId,
                vehicle.LastLatitude ?? 0,
                vehicle.LastLongitude ?? 0,
                callerNewState),
            cancellationToken);
    }

    private static bool IsRepIdle(RepStateRecord? state)
    {
        if (state is null)
            return true;

        var isIdleState = state.State is RepState.Available or RepState.Offline;
        return isIdleState && state.ActiveRequestId is null;
    }

    private static bool IsRepOnActiveJob(RepStateRecord? state)
    {
        if (state is null)
            return false;

        return state.State is RepState.EnRoute or RepState.Within15Miles or RepState.OnSite;
    }
}
