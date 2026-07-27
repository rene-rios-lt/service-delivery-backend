using MediatR;

namespace ServiceDelivery.Application.Features.Dispatcher.Queries;

public record GetDispatcherFleetQuery(Guid DealerId) : IRequest<IReadOnlyList<DispatcherFleetEntryDto>>;

public record DispatcherFleetEntryDto(
    Guid RepId,
    string? Name,
    string State,
    Guid VehicleId,
    string Registration,
    LastPositionDto? LastPosition,
    Guid? ActiveRequestId,
    string? ActiveRequestTier,
    string? ActiveRequestTitle,
    bool HumanControlled,
    DateTime? RedirectCooldownExpiresAt);

public record LastPositionDto(double Lat, double Lng);
