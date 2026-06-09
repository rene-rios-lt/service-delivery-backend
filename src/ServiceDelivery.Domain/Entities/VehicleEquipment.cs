using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Entities;

public class VehicleEquipment
{
    public Guid VehicleId { get; set; }
    public EquipmentType EquipmentType { get; set; }

    public Vehicle? Vehicle { get; set; }
}
