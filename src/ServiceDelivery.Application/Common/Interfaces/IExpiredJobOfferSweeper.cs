namespace ServiceDelivery.Application.Common.Interfaces;

public interface IExpiredJobOfferSweeper
{
    Task SweepAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);
}
