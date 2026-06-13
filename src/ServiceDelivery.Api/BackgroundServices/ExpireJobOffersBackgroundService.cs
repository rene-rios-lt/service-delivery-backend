using Microsoft.Extensions.Options;
using ServiceDelivery.Application.Common.Interfaces;

namespace ServiceDelivery.Api.BackgroundServices;

// Thin timer shell (plan D1): owns scheduling only. The expiry business orchestration lives in
// the scoped IExpiredJobOfferSweeper. Because this hosted service is a singleton and the sweeper
// (with its repositories, matching, hub, and DbContext) is scoped, a fresh DI scope is created
// per tick via IServiceScopeFactory and the sweeper resolved inside it — the scoped graph is
// never injected into the singleton constructor.
public class ExpireJobOffersBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobOfferExpiryOptions _options;
    private readonly ILogger<ExpireJobOffersBackgroundService> _logger;

    public ExpireJobOffersBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<JobOfferExpiryOptions> options,
        ILogger<ExpireJobOffersBackgroundService> logger)
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
                var sweeper = scope.ServiceProvider.GetRequiredService<IExpiredJobOfferSweeper>();
                await sweeper.SweepAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job offer expiry sweep failed; will retry on the next interval.");
            }
        }
    }
}
