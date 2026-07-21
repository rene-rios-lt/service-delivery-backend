using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Common.Services;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Common;

public class ExpiredJobOfferSweeperTests
{
    private readonly Mock<IJobOfferRepository> _jobOffers = new();
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IMatchingService> _matching = new();
    private readonly CapturingLogger<ExpiredJobOfferSweeper> _logger = new();
    private readonly ExpiredJobOfferSweeper _sweeper;

    private static readonly DateTimeOffset AsOf =
        new(2026, 06, 13, 12, 00, 00, TimeSpan.Zero);

    public ExpiredJobOfferSweeperTests()
    {
        _sweeper = new ExpiredJobOfferSweeper(
            _jobOffers.Object,
            _repHub.Object,
            _matching.Object,
            _logger);
    }

    private static JobOffer PendingOffer(Guid? offerId = null, Guid? requestId = null, Guid? repId = null)
        => new()
        {
            Id = offerId ?? Guid.NewGuid(),
            ServiceRequestId = requestId ?? Guid.NewGuid(),
            RepId = repId ?? Guid.NewGuid(),
            OfferedAt = AsOf.UtcDateTime.AddSeconds(-60),
            ExpiresAt = AsOf.UtcDateTime.AddSeconds(-1),
            Status = JobOfferStatus.Pending
        };

    private void SetupExpiredPending(params JobOffer[] offers)
        => _jobOffers
            .Setup(r => r.GetExpiredPendingAsync(AsOf.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offers);

    [Fact]
    public async Task GivenAnOfferPastExpiresAt_WhenSweepRuns_ThenOfferStatusIsExpiredAndPersisted()
    {
        // Arrange
        var offer = PendingOffer();
        SetupExpiredPending(offer);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        offer.Status.Should().Be(JobOfferStatus.Expired);
        _jobOffers.Verify(r => r.UpdateAsync(
            It.Is<JobOffer>(o => o.Id == offer.Id && o.Status == JobOfferStatus.Expired),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenPendingOffersPastExpiry_WhenSweepRuns_ThenEachExpiredOfferIsFetchedAndProcessed()
    {
        // Arrange
        var offer = PendingOffer();
        SetupExpiredPending(offer);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _jobOffers.Verify(r => r.GetExpiredPendingAsync(AsOf.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(r => r.UpdateAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnExpiredOffer_WhenSweepRuns_ThenMatchingServiceRunAsyncIsInvokedForTheRequest()
    {
        // Arrange
        var offer = PendingOffer();
        SetupExpiredPending(offer);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _matching.Verify(m => m.RunAsync(offer.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnExpiredOffer_WhenSweepRuns_ThenJobOfferExpiredSentToRepGroup()
    {
        // Arrange
        var offer = PendingOffer();
        SetupExpiredPending(offer);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _repHub.Verify(h => h.SendJobOfferExpiredAsync(
            $"rep:{offer.RepId}",
            It.Is<JobOfferExpiredPayload>(p => p.OfferId == offer.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnExpiredOffersRep_WhenSweepRuns_ThenOfferIsPersistedExpiredBeforeRematch()
    {
        // Arrange: the offer must be durably persisted as Expired (removing it from the Pending set)
        // BEFORE re-matching, so the request is genuinely orphaned and the re-match does not collide with a
        // still-Pending offer. Per BUG-054 an expired offer no longer skips the rep, so this ordering is
        // about avoiding a duplicate concurrent offer — not about excluding the rep.
        var offer = PendingOffer();
        SetupExpiredPending(offer);
        var sequence = new List<string>();
        _jobOffers
            .Setup(r => r.UpdateAsync(It.Is<JobOffer>(o => o.Status == JobOfferStatus.Expired), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("update"))
            .Returns(Task.CompletedTask);
        _matching
            .Setup(m => m.RunAsync(offer.ServiceRequestId, It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("rematch"))
            .Returns(Task.CompletedTask);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        sequence.Should().Equal("update", "rematch");
    }

    [Fact]
    public async Task GivenMultipleExpiredOffers_WhenSweepRuns_ThenMatchingIsReRunForEachRequest()
    {
        // Arrange
        var offer1 = PendingOffer();
        var offer2 = PendingOffer();
        var offer3 = PendingOffer();
        SetupExpiredPending(offer1, offer2, offer3);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _matching.Verify(m => m.RunAsync(offer1.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);
        _matching.Verify(m => m.RunAsync(offer2.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);
        _matching.Verify(m => m.RunAsync(offer3.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(r => r.UpdateAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GivenOneOfferThrowsDuringExpiry_WhenSweepRuns_ThenRemainingOffersAreStillProcessed()
    {
        // Arrange
        var bad = PendingOffer();
        var good = PendingOffer();
        SetupExpiredPending(bad, good);
        _jobOffers
            .Setup(r => r.UpdateAsync(It.Is<JobOffer>(o => o.Id == bad.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _jobOffers.Verify(r => r.UpdateAsync(
            It.Is<JobOffer>(o => o.Id == good.Id), It.IsAny<CancellationToken>()), Times.Once);
        _matching.Verify(m => m.RunAsync(good.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOfferRacedToNonPending_WhenSweepRuns_ThenExpiryIsSkippedQuietlyAndSweepContinues()
    {
        // Arrange: Phase 1 race — first offer was Accepted in the same instant, so Expire() throws
        // InvalidJobOfferStateException (the expected, benign Accept/Decline race). A second valid
        // offer in the same sweep must still be fully processed.
        var raced = PendingOffer();
        raced.Status = JobOfferStatus.Accepted;
        var good = PendingOffer();
        SetupExpiredPending(raced, good);

        // Act
        var act = () => _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync<InvalidJobOfferStateException>();
        _jobOffers.Verify(r => r.UpdateAsync(
            It.Is<JobOffer>(o => o.Id == raced.Id), It.IsAny<CancellationToken>()), Times.Never);
        _repHub.Verify(h => h.SendJobOfferExpiredAsync(
            $"rep:{raced.RepId}", It.IsAny<JobOfferExpiredPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _matching.Verify(m => m.RunAsync(raced.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Never);
        _jobOffers.Verify(r => r.UpdateAsync(
            It.Is<JobOffer>(o => o.Id == good.Id && o.Status == JobOfferStatus.Expired), It.IsAny<CancellationToken>()), Times.Once);
        _repHub.Verify(h => h.SendJobOfferExpiredAsync(
            $"rep:{good.RepId}", It.IsAny<JobOfferExpiredPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        _matching.Verify(m => m.RunAsync(good.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);

        // Severity contract: the benign race is logged at Information and must NEVER be raised to
        // Warning or Error — that false-alarm avoidance is the whole point of catching it distinctly.
        _logger.HasEntryAt(LogLevel.Information).Should().BeTrue();
        _logger.HasEntryAt(LogLevel.Warning).Should().BeFalse();
        _logger.HasEntryAt(LogLevel.Error).Should().BeFalse();
    }

    [Fact]
    public async Task GivenUpdateAsyncThrows_WhenSweepRuns_ThenNoNotifyOrRematchForThatOfferAndOtherOffersStillProcessed()
    {
        // Arrange: Phase 1 persist failure — the offer is NOT durably Expired, so it must NOT be
        // notified or re-matched (it stays Pending and will be re-swept next tick). Other offers process.
        var bad = PendingOffer();
        var good = PendingOffer();
        SetupExpiredPending(bad, good);
        _jobOffers
            .Setup(r => r.UpdateAsync(It.Is<JobOffer>(o => o.Id == bad.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _repHub.Verify(h => h.SendJobOfferExpiredAsync(
            $"rep:{bad.RepId}", It.IsAny<JobOfferExpiredPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _matching.Verify(m => m.RunAsync(bad.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Never);
        _jobOffers.Verify(r => r.UpdateAsync(
            It.Is<JobOffer>(o => o.Id == good.Id && o.Status == JobOfferStatus.Expired), It.IsAny<CancellationToken>()), Times.Once);
        _matching.Verify(m => m.RunAsync(good.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);

        // Severity contract: a persist failure is recoverable (offer stays Pending, re-swept next
        // tick) — logged at Warning, not Error.
        _logger.HasEntryAt(LogLevel.Warning).Should().BeTrue();
        _logger.HasEntryAt(LogLevel.Error).Should().BeFalse();
    }

    [Fact]
    public async Task GivenSendJobOfferExpiredThrows_WhenSweepRuns_ThenRematchStillRunsForThatRequest()
    {
        // Arrange: Phase 2 notify failure is non-critical and must NOT skip the durability-critical
        // Phase 3 re-match. The offer is already durably Expired at this point.
        var offer = PendingOffer();
        SetupExpiredPending(offer);
        _repHub
            .Setup(h => h.SendJobOfferExpiredAsync(
                $"rep:{offer.RepId}", It.IsAny<JobOfferExpiredPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _matching.Verify(m => m.RunAsync(offer.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);

        // Severity contract: a notify failure is non-critical (the rep UI falls back to its own
        // countdown) — logged at Warning, not Error.
        _logger.HasEntryAt(LogLevel.Warning).Should().BeTrue();
        _logger.HasEntryAt(LogLevel.Error).Should().BeFalse();
    }

    [Fact]
    public async Task GivenRunAsyncThrows_WhenSweepRuns_ThenRemainingOffersAreStillProcessed()
    {
        // Arrange: Phase 3 re-match failure on one offer must be isolated — a subsequent offer
        // still processes fully.
        var bad = PendingOffer();
        var good = PendingOffer();
        SetupExpiredPending(bad, good);
        _matching
            .Setup(m => m.RunAsync(bad.ServiceRequestId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("matching failure"));

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _jobOffers.Verify(r => r.UpdateAsync(
            It.Is<JobOffer>(o => o.Id == good.Id && o.Status == JobOfferStatus.Expired), It.IsAny<CancellationToken>()), Times.Once);
        _repHub.Verify(h => h.SendJobOfferExpiredAsync(
            $"rep:{good.RepId}", It.IsAny<JobOfferExpiredPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        _matching.Verify(m => m.RunAsync(good.ServiceRequestId, It.IsAny<CancellationToken>()), Times.Once);

        // Severity contract: a dropped re-match is the exact stall BE-018 exists to prevent — it MUST
        // stay Error so a potentially orphaned request is alertable. Assert Error specifically.
        _logger.HasEntryAt(LogLevel.Error).Should().BeTrue();
    }

    [Fact]
    public async Task GivenCancellationRequested_WhenAPhaseObservesIt_ThenOperationCanceledPropagatesAndIsNotSwallowed()
    {
        // Arrange: genuine shutdown cancellation must propagate out of the sweep (so the timer shell
        // can break cleanly), not be swallowed by the per-offer phase guards as if it were a failure.
        var offer = PendingOffer();
        SetupExpiredPending(offer);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _jobOffers
            .Setup(r => r.UpdateAsync(It.Is<JobOffer>(o => o.Id == offer.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var act = () => _sweeper.SweepAsync(AsOf, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GivenNoExpiredOffers_WhenSweepRuns_ThenNoMatchingOrHubCallsAreMade()
    {
        // Arrange
        SetupExpiredPending();

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _jobOffers.Verify(r => r.UpdateAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Never);
        _matching.Verify(m => m.RunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _repHub.Verify(h => h.SendJobOfferExpiredAsync(
            It.IsAny<string>(), It.IsAny<JobOfferExpiredPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
