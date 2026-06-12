namespace ServiceDelivery.Application.Common.Interfaces;

public interface IMatchingService
{
    Task RunAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task RunForPendingByDealerAsync(Guid dealerId, CancellationToken cancellationToken = default);
}
