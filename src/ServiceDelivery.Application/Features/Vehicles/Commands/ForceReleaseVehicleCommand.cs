using MediatR;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public record ForceReleaseVehicleCommand(Guid VehicleId) : IRequest<ForceReleaseVehicleResult>;

public record ForceReleaseVehicleResult(Guid VehicleId);
