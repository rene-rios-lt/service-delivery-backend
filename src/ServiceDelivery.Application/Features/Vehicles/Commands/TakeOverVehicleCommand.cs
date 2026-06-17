using MediatR;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public record TakeOverVehicleCommand(Guid VehicleId, Guid RepId) : IRequest<TakeOverVehicleResult>;

public record TakeOverVehicleResult(
    Guid SessionId,
    Guid VehicleId,
    Guid RepId,
    string RepState,
    DateTime StartedAt);
