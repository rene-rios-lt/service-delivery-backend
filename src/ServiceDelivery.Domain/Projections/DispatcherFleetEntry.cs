using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Projections;

public record DispatcherFleetEntry(
    Guid VehicleId,
    string Registration,
    Guid? ClaimingRepId,
    string? RepName,
    RepState? RepState,
    bool HumanControlled,
    double? LastLatitude,
    double? LastLongitude,
    Guid? ActiveRequestId,
    ServiceTier? ActiveRequestTier
);
