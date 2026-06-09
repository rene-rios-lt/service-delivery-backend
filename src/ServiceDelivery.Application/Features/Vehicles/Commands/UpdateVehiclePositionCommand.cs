using MediatR;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public record UpdateVehiclePositionCommand(
    Guid VehicleId,
    Guid SimulatorUserId,
    double Latitude,
    double Longitude,
    DateTime Timestamp) : IRequest<UpdateVehiclePositionResult>;

public record UpdateVehiclePositionResult(Guid VehicleId);
