namespace ServiceDelivery.Api.BackgroundServices;

public class ReconcilePendingRequestsOptions
{
    public int PollIntervalSeconds { get; set; } = 30;
}
