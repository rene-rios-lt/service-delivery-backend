using MediatR;

namespace ServiceDelivery.Application.Features.Vehicles.Queries;

public record GetFleetQuery(Guid DealerId) : IRequest<IReadOnlyList<VehicleDto>>;

public record VehicleDto(
    Guid VehicleId,
    string Registration,
    string State,
    Guid? CurrentRepId,
    IReadOnlyList<string> Equipment,
    LastPositionDto? LastPosition);

public record LastPositionDto(
    double Lat,
    double Lng,
    DateTime UpdatedAt);
