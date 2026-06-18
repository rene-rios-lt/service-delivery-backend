using FluentAssertions;
using Moq;
using MediatR;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Services;
using ServiceDelivery.Application.Features.Rep.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Common;

public class StaleHeartbeatSweeperTests
{
    private readonly Mock<IRepStateRepository> _repStates = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly CapturingLogger<StaleHeartbeatSweeper> _logger = new();
    private readonly HeartbeatTimeoutSettings _settings = new() { TimeoutSeconds = 45 };
    private readonly StaleHeartbeatSweeper _sweeper;

    private static readonly DateTimeOffset AsOf =
        new(2026, 06, 16, 12, 00, 00, TimeSpan.Zero);

    public StaleHeartbeatSweeperTests()
    {
        _sweeper = new StaleHeartbeatSweeper(
            _repStates.Object,
            _users.Object,
            _mediator.Object,
            _settings,
            _logger);
    }

    private void SetupStale(params RepStateRecord[] reps)
        => _repStates
            .Setup(r => r.GetStaleHumanControlledAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reps);

    [Fact]
    public async Task GivenAHumanControlledRepWithStaleHeartbeatAndNoJob_WhenSwept_ThenRepIsMarkedOffline()
    {
        // Arrange
        var repState = new RepStateRecord
        {
            RepId = Guid.NewGuid(),
            State = RepState.Available,
            HumanControlled = true,
            ActiveRequestId = null,
            LastHeartbeatAt = AsOf.UtcDateTime.AddSeconds(-120)
        };
        SetupStale(repState);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        repState.State.Should().Be(RepState.Offline);
    }

    [Fact]
    public async Task GivenAHumanControlledRepWithStaleHeartbeatAndNoJob_WhenSwept_ThenHumanControlledIsCleared()
    {
        // Arrange
        var repState = new RepStateRecord
        {
            RepId = Guid.NewGuid(),
            State = RepState.Available,
            HumanControlled = true,
            ActiveRequestId = null,
            LastHeartbeatAt = AsOf.UtcDateTime.AddSeconds(-120)
        };
        SetupStale(repState);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        repState.HumanControlled.Should().BeFalse();
        _repStates.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == repState.RepId && !s.HumanControlled),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAStaleHumanControlledRepWithNoJob_WhenSwept_ThenNoRepWentOfflineCommandIsSent()
    {
        // Arrange — the no-job case must be handled directly by the sweeper, not delegated
        // (RepWentOfflineCommandHandler returns early when there is no active job).
        var repState = new RepStateRecord
        {
            RepId = Guid.NewGuid(),
            State = RepState.Available,
            HumanControlled = true,
            ActiveRequestId = null,
            LastHeartbeatAt = AsOf.UtcDateTime.AddSeconds(-120)
        };
        SetupStale(repState);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.Send(It.IsAny<RepWentOfflineCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAStaleHumanControlledRepWithActiveJob_WhenSwept_ThenRepWentOfflineCommandIsSentWithDealerId()
    {
        // Arrange — the with-job case must delegate to RepWentOfflineCommand so re-queue + re-match is reused.
        var repId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var repState = new RepStateRecord
        {
            RepId = repId,
            State = RepState.EnRoute,
            HumanControlled = true,
            ActiveRequestId = Guid.NewGuid(),
            LastHeartbeatAt = AsOf.UtcDateTime.AddSeconds(-120)
        };
        SetupStale(repState);
        _users.Setup(u => u.FindByIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = repId, DealerId = dealerId });

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.Send(
            It.Is<RepWentOfflineCommand>(c => c.RepId == repId && c.DealerId == dealerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAStaleHumanControlledRepWithActiveJob_WhenSwept_ThenSweeperDoesNotUpsertDirectly()
    {
        // Arrange — for the with-job case the delegated command owns persistence; the sweeper must not
        // double-write the rep state itself.
        var repId = Guid.NewGuid();
        var repState = new RepStateRecord
        {
            RepId = repId,
            State = RepState.EnRoute,
            HumanControlled = true,
            ActiveRequestId = Guid.NewGuid(),
            LastHeartbeatAt = AsOf.UtcDateTime.AddSeconds(-120)
        };
        SetupStale(repState);
        _users.Setup(u => u.FindByIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = repId, DealerId = Guid.NewGuid() });

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _repStates.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoStaleReps_WhenSwept_ThenNoOfflineActionIsTaken()
    {
        // Arrange — fresh-heartbeat and non-human-controlled reps are excluded by the repository query,
        // so an empty result must produce no writes or commands.
        SetupStale();

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _repStates.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<RepWentOfflineCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenSettingsTimeout_WhenSwept_ThenRepositoryIsQueriedWithCutoffComputedFromAsOf()
    {
        // Arrange — cutoff = asOf - TimeoutSeconds is computed inside the sweeper (SweepAsync stays
        // identical to IExpiredJobOfferSweeper).
        SetupStale();
        var expectedCutoff = AsOf.UtcDateTime.AddSeconds(-_settings.TimeoutSeconds);

        // Act
        await _sweeper.SweepAsync(AsOf, CancellationToken.None);

        // Assert
        _repStates.Verify(r => r.GetStaleHumanControlledAsync(expectedCutoff, It.IsAny<CancellationToken>()), Times.Once);
    }
}
