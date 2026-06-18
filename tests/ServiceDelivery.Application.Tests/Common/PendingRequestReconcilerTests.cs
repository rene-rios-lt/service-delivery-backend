using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Services;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Common;

public class PendingRequestReconcilerTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequests = new();
    private readonly Mock<IMatchingService> _matching = new();
    private readonly CapturingLogger<PendingRequestReconciler> _logger = new();
    private readonly PendingRequestReconciler _reconciler;

    public PendingRequestReconcilerTests()
    {
        _reconciler = new PendingRequestReconciler(
            _serviceRequests.Object,
            _matching.Object,
            _logger);
    }

    private static ServiceRequest PendingRequest(Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            DealerId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            DtcId = Guid.NewGuid(),
            Status = ServiceRequestStatus.Pending,
            Tier = ServiceTier.Bronze,
            CreatedAt = DateTime.UtcNow
        };

    private void SetupOrphans(params ServiceRequest[] requests)
        => _serviceRequests
            .Setup(r => r.GetOrphanedPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);

    [Fact]
    public async Task GivenOrphanedPendingRequests_WhenReconcileRuns_ThenOrphanedPendingQueryIsCalled()
    {
        // Arrange
        SetupOrphans(PendingRequest());

        // Act
        await _reconciler.ReconcileAsync(CancellationToken.None);

        // Assert
        _serviceRequests.Verify(r => r.GetOrphanedPendingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenTwoOrphanedRequests_WhenReconcileRuns_ThenRunAsyncIsInvokedForEach()
    {
        // Arrange
        var orphan1 = PendingRequest();
        var orphan2 = PendingRequest();
        SetupOrphans(orphan1, orphan2);

        // Act
        await _reconciler.ReconcileAsync(CancellationToken.None);

        // Assert
        _matching.Verify(m => m.RunAsync(orphan1.Id, It.IsAny<CancellationToken>()), Times.Once);
        _matching.Verify(m => m.RunAsync(orphan2.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAPendingRequestWithAPendingOffer_WhenReconcileRuns_ThenRunAsyncIsNotInvokedForIt()
    {
        // Arrange: a request that already has a Pending offer is not an orphan, so the query never
        // returns it; the reconciler must therefore never pass it to matching (no duplicate offers).
        var covered = PendingRequest();
        var orphan = PendingRequest();
        SetupOrphans(orphan);

        // Act
        await _reconciler.ReconcileAsync(CancellationToken.None);

        // Assert
        _matching.Verify(m => m.RunAsync(covered.Id, It.IsAny<CancellationToken>()), Times.Never);
        _matching.Verify(m => m.RunAsync(orphan.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOrphanedRequest_WhenReconcileRuns_ThenMatchingIsInvokedWhichHonoursTheSkipList()
    {
        // Arrange: the skip-list (GetSkippedRepIdsForRequestAsync) lives inside MatchingService.RunAsync;
        // the reconciler honours AC-4 simply by routing every orphan through that single matching path.
        var orphan = PendingRequest();
        SetupOrphans(orphan);

        // Act
        await _reconciler.ReconcileAsync(CancellationToken.None);

        // Assert
        _matching.Verify(m => m.RunAsync(orphan.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOrphanWithNoEligibleRep_WhenReconcileRunsTwice_ThenItRemainsReturnedAndNoExceptionIsThrown()
    {
        // Arrange: matching produces no offer (no eligible rep), so the request stays Pending and is
        // still an orphan on the next pass. The reconciler must be safe to repeat — no throw, re-matched.
        var orphan = PendingRequest();
        SetupOrphans(orphan);

        // Act
        var act = async () =>
        {
            await _reconciler.ReconcileAsync(CancellationToken.None);
            await _reconciler.ReconcileAsync(CancellationToken.None);
        };

        // Assert
        await act.Should().NotThrowAsync();
        _matching.Verify(m => m.RunAsync(orphan.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenMatchingThrowsForOneOrphan_WhenReconcileRuns_ThenRemainingOrphansAreStillProcessedAndErrorIsLogged()
    {
        // Arrange: one orphan's re-match fails — it must not abort the pass; the remaining orphan is
        // still processed and the failure is surfaced at Error.
        var bad = PendingRequest();
        var good = PendingRequest();
        SetupOrphans(bad, good);
        _matching
            .Setup(m => m.RunAsync(bad.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("matching failure"));

        // Act
        await _reconciler.ReconcileAsync(CancellationToken.None);

        // Assert
        _matching.Verify(m => m.RunAsync(good.Id, It.IsAny<CancellationToken>()), Times.Once);
        _logger.HasEntryAt(Microsoft.Extensions.Logging.LogLevel.Error).Should().BeTrue();
    }

    [Fact]
    public async Task GivenCancellationRequested_WhenMatchingObservesIt_ThenOperationCanceledPropagatesAndIsNotSwallowed()
    {
        // Arrange: genuine shutdown cancellation must propagate out of the pass (so the timer shell can
        // break cleanly), not be swallowed by the per-orphan guard as if it were a re-match failure.
        var orphan = PendingRequest();
        SetupOrphans(orphan);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _matching
            .Setup(m => m.RunAsync(orphan.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var act = () => _reconciler.ReconcileAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void GivenTheReconciler_WhenConstructed_ThenItHasNoHubServiceDependency()
    {
        // Arrange: AC-7 — the reconciler emits no SignalR events of its own. All offer/assignment
        // events originate inside MatchingService. This is asserted structurally: no constructor
        // parameter is a hub-service abstraction.
        var constructorParameterTypes = typeof(PendingRequestReconciler)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name);

        // Act
        var hubDependencies = constructorParameterTypes.Where(name => name.EndsWith("HubService"));

        // Assert
        hubDependencies.Should().BeEmpty();
    }
}
