namespace ServiceDelivery.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public Guid DealerId { get; set; }
    public Guid? ClaimedByRepId { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public string Registration { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastPositionUpdatedAt { get; set; }

    public ICollection<VehicleEquipment> Equipment { get; set; } = new List<VehicleEquipment>();
}
