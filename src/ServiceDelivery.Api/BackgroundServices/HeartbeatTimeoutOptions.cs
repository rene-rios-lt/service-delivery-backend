namespace ServiceDelivery.Api.BackgroundServices;

public class HeartbeatTimeoutOptions
{
    public int PollIntervalSeconds { get; set; } = 10;
}
