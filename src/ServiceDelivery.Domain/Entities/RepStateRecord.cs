using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Entities;

public class RepStateRecord
{
    public Guid RepId { get; set; }
    public RepState State { get; set; } = RepState.Offline;
    public Guid? ActiveRequestId { get; set; }
    public DateTime? LastRedirectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void GoEnRoute(Guid requestId)
    {
        State = RepState.EnRoute;
        ActiveRequestId = requestId;
        UpdatedAt = DateTime.UtcNow;
    }
}
