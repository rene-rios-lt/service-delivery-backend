using Microsoft.Extensions.Options;
using ServiceDelivery.Application.Common.Interfaces;

namespace ServiceDelivery.Api.BackgroundServices;

// Thin timer shell (mirrors ExpireJobOffersBackgroundService): owns scheduling only. The stale-heartbeat
// business orchestration lives in the scoped IStaleHeartbeatSweeper. Because this hosted service is a
// singleton and the sweeper (with its repositories, mediator, and DbContext) is scoped, a fresh DI scope
// is created per tick via IServiceScopeFactory and the sweeper resolved inside it — the scoped graph is
// never injected into the singleton constructor.
public class HeartbeatTimeoutBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HeartbeatTimeoutOptions _options;
    private readonly ILogger<HeartbeatTimeoutBackgroundService> _logger;

    public HeartbeatTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<HeartbeatTimeoutOptions> options,
        ILogger<HeartbeatTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            // Top-level guard: a sweep failure (DB outage, etc.) must never kill the loop.
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sweeper = scope.ServiceProvider.GetRequiredService<IStaleHeartbeatSweeper>();
                await sweeper.SweepAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stale heartbeat sweep failed; will retry on the next interval.");
            }
        }
    }
}
