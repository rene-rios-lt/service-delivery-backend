using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Projections;

public record RepMatchCandidate(
    Guid RepId,
    double VehicleLatitude,
    double VehicleLongitude,
    IReadOnlyList<EquipmentType> Equipment,
    DateTime AvailableSince);
