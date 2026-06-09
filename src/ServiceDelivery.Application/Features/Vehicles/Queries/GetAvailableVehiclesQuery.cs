using MediatR;

namespace ServiceDelivery.Application.Features.Vehicles.Queries;

public record GetAvailableVehiclesQuery(Guid DealerId) : IRequest<IReadOnlyList<AvailableVehicleDto>>;

public record AvailableVehicleDto(
    Guid VehicleId,
    string Registration,
    IReadOnlyList<string> Equipment);
