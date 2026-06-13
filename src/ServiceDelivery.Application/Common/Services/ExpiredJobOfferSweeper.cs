using Microsoft.Extensions.Logging;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Common.Services;

public class ExpiredJobOfferSweeper : IExpiredJobOfferSweeper
{
    private readonly IJobOfferRepository _jobOffers;
    private readonly IRepHubService _repHub;
    private readonly IMatchingService _matching;
    private readonly ILogger<ExpiredJobOfferSweeper> _logger;

    public ExpiredJobOfferSweeper(
        IJobOfferRepository jobOffers,
        IRepHubService repHub,
        IMatchingService matching,
        ILogger<ExpiredJobOfferSweeper> logger)
    {
        _jobOffers = jobOffers;
        _repHub = repHub;
        _matching = matching;
        _logger = logger;
    }

    public async Task SweepAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        var expiredPending = await _jobOffers.GetExpiredPendingAsync(asOf.UtcDateTime, cancellationToken);

        foreach (var offer in expiredPending)
            await ExpireOfferAsync(offer, cancellationToken);
    }

    private async Task ExpireOfferAsync(JobOffer offer, CancellationToken cancellationToken)
    {
        // The catch blocks below are split at the PERSIST BOUNDARY so each concern is isolated and
        // logged at a severity that matches its real cost. One offer failing must never abort the sweep.

        // PHASE 1 — transition + persist (the recoverable boundary).
        if (!await TryExpireAndPersistAsync(offer, cancellationToken))
            return;

        // Reaching here means the offer is now durably Expired. The two side effects below are
        // INDEPENDENT and best-effort — one failing must not skip the other.

        // PHASE 2 — notify the rep (non-critical).
        await NotifyRepAsync(offer, cancellationToken);

        // PHASE 3 — re-run matching (durability-critical).
        await ReRunMatchingAsync(offer, cancellationToken);
    }

    private async Task<bool> TryExpireAndPersistAsync(JobOffer offer, CancellationToken cancellationToken)
    {
        try
        {
            offer.Expire();
            await _jobOffers.UpdateAsync(offer, cancellationToken);
            return true;
        }
        catch (InvalidJobOfferStateException)
        {
            // Expected, benign Accept/Decline race: the offer is no longer Pending. Not a failure.
            _logger.LogInformation(
                "Offer {OfferId} no longer pending (raced with accept/decline); skipping expiry.",
                offer.Id);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // genuine shutdown cancellation — let it propagate so the timer shell stops cleanly.
        }
        catch (Exception ex)
        {
            // Persist failed: the offer was NOT stored as Expired, so it remains Pending and
            // GetExpiredPendingAsync will return it on the next sweep — naturally recoverable.
            _logger.LogWarning(
                ex,
                "Could not persist expiry for offer {OfferId}; will retry next sweep.",
                offer.Id);
            return false;
        }
    }

    private async Task NotifyRepAsync(JobOffer offer, CancellationToken cancellationToken)
    {
        try
        {
            await _repHub.SendJobOfferExpiredAsync(
                $"rep:{offer.RepId}",
                new JobOfferExpiredPayload(offer.Id),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Rep {RepId} was not notified that offer {OfferId} expired (countdown UI will fall back to its own timeout).",
                offer.RepId,
                offer.Id);
        }
    }

    private async Task ReRunMatchingAsync(JobOffer offer, CancellationToken cancellationToken)
    {
        try
        {
            await _matching.RunAsync(offer.ServiceRequestId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Higher severity than the other phases: a dropped re-match is the exact failure
            // BE-018 exists to prevent. Surfaced here; full orphaned-request reconciliation is out of scope.
            _logger.LogError(
                ex,
                "Re-match failed for request {RequestId} after offer {OfferId} expired; request may remain unassigned until the next submission/matching trigger.",
                offer.ServiceRequestId,
                offer.Id);
        }
    }
}
