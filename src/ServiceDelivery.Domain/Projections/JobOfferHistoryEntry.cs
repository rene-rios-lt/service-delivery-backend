using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Projections;

public record JobOfferHistoryEntry(
    Guid OfferId,
    Guid RepId,
    string RepName,
    JobOfferStatus Status,
    DateTime OfferedAt,
    DateTime ExpiresAt
);
