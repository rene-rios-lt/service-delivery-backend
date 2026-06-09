using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Entities;

public class JobOffer
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public Guid RepId { get; set; }
    public DateTime OfferedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public JobOfferStatus Status { get; set; } = JobOfferStatus.Pending;
}
