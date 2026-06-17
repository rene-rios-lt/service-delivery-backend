using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Projections;

public record FleetJobState(
    Guid VehicleId,
    Guid? ClaimingRepId,
    RepState? RepState,
    bool HumanControlled,
    double? ActiveRequestLatitude,
    double? ActiveRequestLongitude
);
