using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Entities;

public class ServiceRequest
{
    public Guid Id { get; set; }
    public Guid DealerId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid DtcId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;
    public ServiceTier Tier { get; set; }
    public Guid? AssignedRepId { get; set; }
    public DateTime CreatedAt { get; set; }

    public void AssignTo(Guid repId)
    {
        if (Status != ServiceRequestStatus.Pending)
            throw new InvalidJobOfferStateException(
                $"Service request {Id} cannot be assigned from state {Status}; only a Pending request can be assigned.");

        Status = ServiceRequestStatus.Assigned;
        AssignedRepId = repId;
    }

    public void MarkInProgress()
    {
        if (Status != ServiceRequestStatus.Assigned)
            throw new InvalidServiceRequestStateException(
                $"Service request {Id} cannot be marked in progress from state {Status}; only an Assigned request can be marked in progress.");

        Status = ServiceRequestStatus.InProgress;
    }

    public void MarkCompleted()
    {
        if (Status != ServiceRequestStatus.InProgress)
            throw new InvalidServiceRequestStateException(
                $"Service request {Id} cannot be completed from state {Status}; only an InProgress request can be completed.");

        Status = ServiceRequestStatus.Completed;
    }
}
