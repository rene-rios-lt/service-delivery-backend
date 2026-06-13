using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Entities;

public class JobOffer
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public Guid RepId { get; set; }
    public DateTime OfferedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public JobOfferStatus Status { get; set; } = JobOfferStatus.Pending;

    public void Accept()
    {
        if (Status != JobOfferStatus.Pending)
            throw new InvalidJobOfferStateException(
                $"Job offer {Id} cannot be accepted from state {Status}; only a Pending offer can be accepted.");

        Status = JobOfferStatus.Accepted;
    }

    public void Decline()
    {
        if (Status != JobOfferStatus.Pending)
            throw new InvalidJobOfferStateException(
                $"Job offer {Id} cannot be declined from state {Status}; only a Pending offer can be declined.");

        Status = JobOfferStatus.Declined;
    }
}
