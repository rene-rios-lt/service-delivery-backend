using MediatR;

namespace ServiceDelivery.Application.Features.Simulator.Queries;

public record GetFleetStateQuery(Guid DealerId) : IRequest<IReadOnlyList<FleetStateVehicleDto>>;

public record FleetStateVehicleDto(
    Guid VehicleId,
    Guid? ClaimingRepId,
    string RepState,
    bool HumanControlled,
    ActiveRequestLocationDto? ActiveRequestLocation);

public record ActiveRequestLocationDto(double Lat, double Lng);
