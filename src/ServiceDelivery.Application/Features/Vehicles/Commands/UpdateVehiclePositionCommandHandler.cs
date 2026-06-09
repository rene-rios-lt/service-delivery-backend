using MediatR;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public class UpdateVehiclePositionCommandHandler : IRequestHandler<UpdateVehiclePositionCommand, UpdateVehiclePositionResult>
{
    private const double ThresholdMiles = 15.0;

    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRepStateRepository _repStateRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IVehiclePositionHubService _vehiclePositionHubService;
    private readonly IRequesterHubService _requesterHubService;

    public UpdateVehiclePositionCommandHandler(
        IVehicleRepository vehicleRepository,
        IRepStateRepository repStateRepository,
        IServiceRequestRepository serviceRequestRepository,
        IVehiclePositionHubService vehiclePositionHubService,
        IRequesterHubService requesterHubService)
    {
        _vehicleRepository = vehicleRepository;
        _repStateRepository = repStateRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _vehiclePositionHubService = vehiclePositionHubService;
        _requesterHubService = requesterHubService;
    }

    public async Task<UpdateVehiclePositionResult> Handle(UpdateVehiclePositionCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");

        vehicle.LastLatitude = request.Latitude;
        vehicle.LastLongitude = request.Longitude;
        vehicle.LastPositionUpdatedAt = request.Timestamp;
        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);

        var repId = vehicle.ClaimedByRepId;
        var repState = repId.HasValue
            ? await _repStateRepository.GetByRepIdAsync(repId.Value, cancellationToken)
            : null;

        await BroadcastVehiclePositionAsync(vehicle, repId, repState, cancellationToken);

        if (repId.HasValue && repState?.State == RepState.EnRoute)
        {
            var activeRequest = await _serviceRequestRepository.GetActiveByRepIdAsync(repId.Value, cancellationToken);

            if (activeRequest is not null)
            {
                var distanceMiles = HaversineCalculator.DistanceMiles(
                    request.Latitude, request.Longitude,
                    activeRequest.Latitude, activeRequest.Longitude);

                var newState = distanceMiles < ThresholdMiles ? RepState.Within15Miles : RepState.EnRoute;
                repState.State = newState;
                repState.UpdatedAt = DateTime.UtcNow;
                await _repStateRepository.UpsertAsync(repState, cancellationToken);

                if (activeRequest.Status == ServiceRequestStatus.Assigned)
                {
                    var etaMinutes = HaversineCalculator.EtaMinutes(distanceMiles);
                    var requesterGroup = $"requester:{activeRequest.RequesterId}";
                    var repPositionPayload = new RepPositionUpdatedPayload(
                        request.Latitude,
                        request.Longitude,
                        etaMinutes,
                        newState.ToString());
                    await _requesterHubService.SendRepPositionUpdatedAsync(requesterGroup, repPositionPayload, cancellationToken);
                }
            }
        }

        return new UpdateVehiclePositionResult(vehicle.Id);
    }

    private async Task BroadcastVehiclePositionAsync(
        Domain.Entities.Vehicle vehicle,
        Guid? repId,
        Domain.Entities.RepStateRecord? repState,
        CancellationToken cancellationToken)
    {
        var dealerGroup = $"dealer:{vehicle.DealerId}";
        var vehiclePayload = new VehiclePositionUpdatedPayload(
            repId ?? Guid.Empty,
            vehicle.Id,
            vehicle.LastLatitude ?? 0,
            vehicle.LastLongitude ?? 0,
            repState?.State.ToString() ?? "Unassigned");
        await _vehiclePositionHubService.SendVehiclePositionUpdatedAsync(dealerGroup, vehiclePayload, cancellationToken);
    }
}
