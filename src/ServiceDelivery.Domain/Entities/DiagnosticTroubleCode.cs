using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Entities;

public class DiagnosticTroubleCode
{
    public Guid Id { get; set; }
    public Guid DealerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string HumanReadableTitle { get; set; } = string.Empty;
    public EquipmentType RequiredEquipmentType { get; set; }
}
