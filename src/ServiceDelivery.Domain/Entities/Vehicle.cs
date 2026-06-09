namespace ServiceDelivery.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public Guid DealerId { get; set; }
    public Guid? ClaimedByRepId { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public string Registration { get; set; } = string.Empty;

    public ICollection<VehicleEquipment> Equipment { get; set; } = new List<VehicleEquipment>();
}
