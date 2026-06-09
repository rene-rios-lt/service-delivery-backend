namespace ServiceDelivery.Domain.Entities;

public class RepSession
{
    public Guid Id { get; set; }
    public Guid RepId { get; set; }
    public Guid VehicleId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
