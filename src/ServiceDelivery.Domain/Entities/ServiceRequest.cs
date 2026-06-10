using ServiceDelivery.Domain.Enums;

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
}
