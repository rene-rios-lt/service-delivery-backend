namespace ServiceDelivery.Application.Common.Interfaces;

public interface IStaleHeartbeatSweeper
{
    Task SweepAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);
}
