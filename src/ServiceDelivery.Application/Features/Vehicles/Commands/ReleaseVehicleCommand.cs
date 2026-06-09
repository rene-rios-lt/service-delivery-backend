using MediatR;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public record ReleaseVehicleCommand(Guid VehicleId, Guid RepId) : IRequest<ReleaseVehicleResult>;

public record ReleaseVehicleResult(Guid VehicleId);
