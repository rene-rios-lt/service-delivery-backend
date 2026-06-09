using MediatR;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public class ClaimVehicleCommandHandler : IRequestHandler<ClaimVehicleCommand, ClaimVehicleResult>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRepSessionRepository _repSessionRepository;
    private readonly IRepStateRepository _repStateRepository;

    public ClaimVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IRepSessionRepository repSessionRepository,
        IRepStateRepository repStateRepository)
    {
        _vehicleRepository = vehicleRepository;
        _repSessionRepository = repSessionRepository;
        _repStateRepository = repStateRepository;
    }

    public async Task<ClaimVehicleResult> Handle(ClaimVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");

        if (vehicle.ClaimedByRepId is not null)
            throw new VehicleAlreadyClaimedException(request.VehicleId);

        var existingSession = await _repSessionRepository.GetActiveByRepIdAsync(request.RepId, cancellationToken);
        if (existingSession is not null)
            throw new RepAlreadyHasActiveSessionException(request.RepId);

        var now = DateTime.UtcNow;

        vehicle.ClaimedByRepId = request.RepId;
        vehicle.ClaimedAt = now;
        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);

        var session = new RepSession
        {
            Id = Guid.NewGuid(),
            RepId = request.RepId,
            VehicleId = request.VehicleId,
            StartedAt = now
        };
        await _repSessionRepository.AddAsync(session, cancellationToken);

        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken)
            ?? new RepStateRecord { RepId = request.RepId };

        repState.State = RepState.Available;
        repState.UpdatedAt = now;
        await _repStateRepository.UpsertAsync(repState, cancellationToken);

        return new ClaimVehicleResult(vehicle.Id, request.RepId, now);
    }
}
