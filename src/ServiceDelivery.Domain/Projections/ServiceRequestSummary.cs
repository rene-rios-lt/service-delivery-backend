using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Projections;

public record ServiceRequestSummary(
    Guid RequestId,
    string RequesterName,
    ServiceTier Tier,
    string DtcTitle,
    ServiceRequestStatus Status,
    Guid? AssignedRepId,
    string? AssignedRepName,
    DateTime CreatedAt
);
