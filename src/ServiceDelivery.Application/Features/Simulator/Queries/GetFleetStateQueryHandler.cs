using MediatR;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Simulator.Queries;

public class GetFleetStateQueryHandler : IRequestHandler<GetFleetStateQuery, IReadOnlyList<FleetStateVehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;

    public GetFleetStateQueryHandler(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<IReadOnlyList<FleetStateVehicleDto>> Handle(
        GetFleetStateQuery request,
        CancellationToken cancellationToken)
    {
        var jobStates = await _vehicleRepository.GetFleetJobStateByDealerAsync(
            request.DealerId, cancellationToken);

        return jobStates.Select(js =>
        {
            ActiveRequestLocationDto? location =
                js.ActiveRequestLatitude.HasValue && js.ActiveRequestLongitude.HasValue
                    ? new ActiveRequestLocationDto(js.ActiveRequestLatitude.Value, js.ActiveRequestLongitude.Value)
                    : null;

            return new FleetStateVehicleDto(
                js.VehicleId,
                js.ClaimingRepId,
                (js.RepState ?? RepState.Offline).ToString(),
                js.HumanControlled,
                location);
        }).ToList();
    }
}
