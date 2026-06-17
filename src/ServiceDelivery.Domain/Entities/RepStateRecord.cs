using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Entities;

public class RepStateRecord
{
    public Guid RepId { get; set; }
    public RepState State { get; set; } = RepState.Offline;
    public Guid? ActiveRequestId { get; set; }
    public bool HumanControlled { get; set; }
    public DateTime? LastRedirectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void GoEnRoute(Guid requestId)
    {
        State = RepState.EnRoute;
        ActiveRequestId = requestId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void GoOnSite()
    {
        if (State != RepState.Within15Miles)
            throw new InvalidRepStateException(
                $"Rep {RepId} cannot go on site from state {State}; only a Within15Miles rep can arrive on site.");

        State = RepState.OnSite;
        UpdatedAt = DateTime.UtcNow;
    }

    public void GoAvailable()
    {
        if (State != RepState.OnSite)
            throw new InvalidRepStateException(
                $"Rep {RepId} cannot go available from state {State}; only an OnSite rep can complete a job.");

        State = RepState.Available;
        ActiveRequestId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void GoOffline()
    {
        State = RepState.Offline;
        ActiveRequestId = null;
        HumanControlled = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
