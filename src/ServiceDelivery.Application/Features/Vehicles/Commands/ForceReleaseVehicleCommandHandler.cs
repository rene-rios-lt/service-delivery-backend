using MediatR;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public class ForceReleaseVehicleCommandHandler : IRequestHandler<ForceReleaseVehicleCommand, ForceReleaseVehicleResult>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRepSessionRepository _repSessionRepository;
    private readonly IRepStateRepository _repStateRepository;
    private readonly IRepHubService _repHubService;

    public ForceReleaseVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IRepSessionRepository repSessionRepository,
        IRepStateRepository repStateRepository,
        IRepHubService repHubService)
    {
        _vehicleRepository = vehicleRepository;
        _repSessionRepository = repSessionRepository;
        _repStateRepository = repStateRepository;
        _repHubService = repHubService;
    }

    public async Task<ForceReleaseVehicleResult> Handle(ForceReleaseVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");

        var repId = vehicle.ClaimedByRepId;

        if (repId is not null)
        {
            var session = await _repSessionRepository.GetActiveByRepIdAsync(repId.Value, cancellationToken);
            if (session is not null)
            {
                session.EndedAt = DateTime.UtcNow;
                await _repSessionRepository.UpdateAsync(session, cancellationToken);
            }

            var repState = await _repStateRepository.GetByRepIdAsync(repId.Value, cancellationToken);
            if (repState is not null)
            {
                repState.State = RepState.Offline;
                repState.UpdatedAt = DateTime.UtcNow;
                await _repStateRepository.UpsertAsync(repState, cancellationToken);

                var payload = new VehicleForceReleasedPayload(vehicle.Id, vehicle.Registration);
                await _repHubService.SendVehicleForceReleasedAsync($"rep:{repId}", payload, cancellationToken);
            }
        }

        vehicle.ClaimedByRepId = null;
        vehicle.ClaimedAt = null;
        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);

        return new ForceReleaseVehicleResult(vehicle.Id);
    }
}
