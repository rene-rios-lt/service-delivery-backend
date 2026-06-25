using MediatR;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Vehicles.Queries;

public class GetAvailableVehiclesQueryHandler : IRequestHandler<GetAvailableVehiclesQuery, IReadOnlyList<AvailableVehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRepStateRepository _repStateRepository;

    public GetAvailableVehiclesQueryHandler(
        IVehicleRepository vehicleRepository,
        IRepStateRepository repStateRepository)
    {
        _vehicleRepository = vehicleRepository;
        _repStateRepository = repStateRepository;
    }

    public async Task<IReadOnlyList<AvailableVehicleDto>> Handle(
        GetAvailableVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        // A vehicle is available for take-over if it is unclaimed OR claimed by an idle rep — the
        // human supersedes the simulator on idle vehicles (FE-007). The simulator claims all
        // vehicles, so filtering to unclaimed-only would always return an empty list (BUG-027).
        var vehicles = await _vehicleRepository.GetAllByDealerIdAsync(request.DealerId, cancellationToken);

        var available = new List<AvailableVehicleDto>();
        foreach (var vehicle in vehicles)
        {
            if (!await IsTakeableAsync(vehicle, cancellationToken))
                continue;

            available.Add(new AvailableVehicleDto(
                vehicle.Id,
                vehicle.Registration,
                vehicle.Equipment.Select(e => e.EquipmentType.ToString()).ToList()));
        }

        return available;
    }

    private async Task<bool> IsTakeableAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        if (vehicle.ClaimedByRepId is null)
            return true;

        var repState = await _repStateRepository.GetByRepIdAsync(vehicle.ClaimedByRepId.Value, cancellationToken);

        // No state record (never went on duty) or a rep not on an active job → the vehicle is idle
        // and can be taken over. Mirrors the TakeOverVehicleCommandHandler's own idle guard.
        return repState is null || !repState.IsOnActiveJob();
    }
}
