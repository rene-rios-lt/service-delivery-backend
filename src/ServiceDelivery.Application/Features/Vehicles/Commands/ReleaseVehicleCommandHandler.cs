using MediatR;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public class ReleaseVehicleCommandHandler : IRequestHandler<ReleaseVehicleCommand, ReleaseVehicleResult>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRepSessionRepository _repSessionRepository;
    private readonly IRepStateRepository _repStateRepository;

    public ReleaseVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IRepSessionRepository repSessionRepository,
        IRepStateRepository repStateRepository)
    {
        _vehicleRepository = vehicleRepository;
        _repSessionRepository = repSessionRepository;
        _repStateRepository = repStateRepository;
    }

    public async Task<ReleaseVehicleResult> Handle(ReleaseVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");

        if (vehicle.ClaimedByRepId != request.RepId)
            throw new VehicleNotClaimedByRepException(request.VehicleId);

        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken);
        if (repState?.State == RepState.OnSite)
            throw new VehicleReleaseBlockedByActiveJobException(request.RepId);

        var session = await _repSessionRepository.GetActiveByRepIdAsync(request.RepId, cancellationToken);
        if (session is not null)
        {
            session.EndedAt = DateTime.UtcNow;
            await _repSessionRepository.UpdateAsync(session, cancellationToken);
        }

        vehicle.ClaimedByRepId = null;
        vehicle.ClaimedAt = null;
        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);

        if (repState is not null)
        {
            repState.State = RepState.Offline;
            repState.UpdatedAt = DateTime.UtcNow;
            await _repStateRepository.UpsertAsync(repState, cancellationToken);
        }

        return new ReleaseVehicleResult(request.VehicleId);
    }
}
