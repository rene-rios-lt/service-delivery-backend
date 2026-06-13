namespace ServiceDelivery.Api.BackgroundServices;

public class JobOfferExpiryOptions
{
    public int PollIntervalSeconds { get; set; } = 10;
}
