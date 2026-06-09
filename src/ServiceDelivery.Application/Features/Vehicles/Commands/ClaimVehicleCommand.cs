using MediatR;

namespace ServiceDelivery.Application.Features.Vehicles.Commands;

public record ClaimVehicleCommand(Guid VehicleId, Guid RepId) : IRequest<ClaimVehicleResult>;

public record ClaimVehicleResult(Guid VehicleId, Guid RepId, DateTime ClaimedAt);
