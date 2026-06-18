using Microsoft.Extensions.Options;
using ServiceDelivery.Application.Common.Interfaces;

namespace ServiceDelivery.Api.BackgroundServices;

// Thin timer shell (mirrors ExpireJobOffersBackgroundService): owns scheduling only. The reconcile
// orchestration lives in the scoped IPendingRequestReconciler. Because this hosted service is a
// singleton and the reconciler (with its repository and matching service) is scoped, a fresh DI
// scope is created per tick via IServiceScopeFactory and the reconciler resolved inside it — the
// scoped graph is never injected into the singleton constructor.
public class ReconcilePendingRequestsBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReconcilePendingRequestsOptions _options;
    private readonly ILogger<ReconcilePendingRequestsBackgroundService> _logger;

    public ReconcilePendingRequestsBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReconcilePendingRequestsOptions> options,
        ILogger<ReconcilePendingRequestsBackgroundService> logger)
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
            // Top-level guard: a reconcile failure (DB outage, etc.) must never kill the loop.
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<IPendingRequestReconciler>();
                await reconciler.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending request reconcile pass failed; will retry on the next interval.");
            }
        }
    }
}
