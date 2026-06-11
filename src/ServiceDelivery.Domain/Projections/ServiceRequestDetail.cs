using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Projections;

public record ServiceRequestDetail(
    Guid RequestId,
    Guid RequesterId,
    string RequesterName,
    ServiceTier Tier,
    string DtcTitle,
    double Latitude,
    double Longitude,
    ServiceRequestStatus Status,
    Guid? AssignedRepId,
    string? AssignedRepName,
    DateTime CreatedAt,
    IReadOnlyList<JobOfferHistoryEntry> OfferHistory
);
