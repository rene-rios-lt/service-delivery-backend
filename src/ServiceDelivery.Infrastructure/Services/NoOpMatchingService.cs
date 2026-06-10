using ServiceDelivery.Application.Common.Interfaces;

namespace ServiceDelivery.Infrastructure.Services;

public class NoOpMatchingService : IMatchingService
{
    public Task RunAsync(Guid requestId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
