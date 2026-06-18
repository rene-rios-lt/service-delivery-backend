namespace ServiceDelivery.Application.Common.Interfaces;

// Plain Application-layer settings carrying the staleness threshold the StaleHeartbeatSweeper needs.
// It lives here (not in Api, where HeartbeatTimeoutOptions owns the timer cadence) so Application never
// references Api — bound in the composition root from the same "HeartbeatTimeout" config section.
public class HeartbeatTimeoutSettings
{
    public int TimeoutSeconds { get; set; } = 45;
}
