using Microsoft.Extensions.Logging;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Common.Services;

public class PendingRequestReconciler : IPendingRequestReconciler
{
    private readonly IServiceRequestRepository _serviceRequests;
    private readonly IMatchingService _matching;
    private readonly ILogger<PendingRequestReconciler> _logger;

    public PendingRequestReconciler(
        IServiceRequestRepository serviceRequests,
        IMatchingService matching,
        ILogger<PendingRequestReconciler> logger)
    {
        _serviceRequests = serviceRequests;
        _matching = matching;
        _logger = logger;
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var orphans = await _serviceRequests.GetOrphanedPendingAsync(cancellationToken);

        foreach (var orphan in orphans)
            await ReRunMatchingAsync(orphan, cancellationToken);
    }

    private async Task ReRunMatchingAsync(ServiceRequest orphan, CancellationToken cancellationToken)
    {
        // Per-orphan isolation (mirrors ExpiredJobOfferSweeper.ReRunMatchingAsync): one failing
        // request must never abort the pass. A failed re-match leaves the request Pending, so it is
        // returned again next pass — the reconciler is idempotent by construction.
        try
        {
            await _matching.RunAsync(orphan.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // genuine shutdown cancellation — let it propagate so the timer shell stops cleanly.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Reconcile re-match failed for orphaned request {RequestId}; it remains Pending and will be retried on the next pass.",
                orphan.Id);
        }
    }
}
