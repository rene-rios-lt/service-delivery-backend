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
    public DateTime? LastHeartbeatAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void GoEnRoute(Guid requestId)
    {
        State = RepState.EnRoute;
        ActiveRequestId = requestId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Redirect(Guid newRequestId, DateTime now)
    {
        State = RepState.EnRoute;
        ActiveRequestId = newRequestId;
        LastRedirectedAt = now;
        UpdatedAt = now;
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

    /// <summary>
    /// True when the rep is actively servicing a job (EnRoute, Within15Miles, or OnSite). A vehicle
    /// claimed by such a rep cannot be taken over; an idle rep (Available/Offline) can be superseded.
    /// </summary>
    public bool IsOnActiveJob() =>
        State is RepState.EnRoute or RepState.Within15Miles or RepState.OnSite;

    public void RecordHeartbeat()
    {
        var now = DateTime.UtcNow;
        LastHeartbeatAt = now;
        UpdatedAt = now;
    }

    public void TakeOver()
    {
        var now = DateTime.UtcNow;
        State = RepState.Available;
        ActiveRequestId = null;
        HumanControlled = true;
        LastHeartbeatAt = now;
        UpdatedAt = now;
    }
}
