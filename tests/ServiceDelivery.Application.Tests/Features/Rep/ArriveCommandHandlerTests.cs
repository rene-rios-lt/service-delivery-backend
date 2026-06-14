using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Rep.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Rep;

public class ArriveCommandHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepository = new();
    private readonly Mock<IRepStateRepository> _repStateRepository = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();
    private readonly Mock<IRequesterHubService> _requesterHub = new();
    private readonly ArriveCommandHandler _handler;

    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid RepId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid DealerId = Guid.NewGuid();

    public ArriveCommandHandlerTests()
    {
        _handler = new ArriveCommandHandler(
            _serviceRequestRepository.Object,
            _repStateRepository.Object,
            _dispatchHub.Object,
            _requesterHub.Object);
    }

    private void SetupHappyPath(
        ServiceRequestStatus requestStatus = ServiceRequestStatus.Assigned,
        RepState repState = RepState.Within15Miles)
    {
        var request = new ServiceRequest
        {
            Id = RequestId,
            DealerId = DealerId,
            RequesterId = RequesterId,
            DtcId = Guid.NewGuid(),
            Latitude = 41.6,
            Longitude = -93.6,
            Status = requestStatus,
            Tier = ServiceTier.Gold,
            AssignedRepId = RepId,
            CreatedAt = DateTime.UtcNow
        };
        var repStateRecord = new RepStateRecord
        {
            RepId = RepId,
            State = repState,
            ActiveRequestId = RequestId,
            UpdatedAt = DateTime.UtcNow
        };

        _serviceRequestRepository.Setup(r => r.GetActiveByRepIdAsync(RepId, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repStateRepository.Setup(r => r.GetByRepIdAsync(RepId, It.IsAny<CancellationToken>())).ReturnsAsync(repStateRecord);
    }

    private ArriveCommand Command() => new(RepId);

    [Fact]
    public async Task GivenAWithin15MilesRepWithActiveRequest_WhenArriveHandled_ThenRepStateIsUpsertedAsOnSite()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _repStateRepository.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == RepId && s.State == RepState.OnSite),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAWithin15MilesRepWithActiveRequest_WhenArriveHandled_ThenRequestIsPersistedAsInProgress()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _serviceRequestRepository.Verify(r => r.UpdateAsync(
            It.Is<ServiceRequest>(s => s.Id == RequestId && s.Status == ServiceRequestStatus.InProgress),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAWithin15MilesRepWithActiveRequest_WhenArriveHandled_ThenResultReflectsTransitionedStates()
    {
        // Arrange
        SetupHappyPath();

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.RepId.Should().Be(RepId);
        result.RequestId.Should().Be(RequestId);
        result.RepState.Should().Be(RepState.OnSite.ToString());
        result.RequestStatus.Should().Be(ServiceRequestStatus.InProgress.ToString());
    }

    [Fact]
    public async Task GivenAWithin15MilesRepWithActiveRequest_WhenArriveHandled_ThenSendRepStateChangedIsInvokedWithOldAndNewState()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(
            $"dealer:{DealerId}",
            It.Is<RepStateChangedPayload>(p => p.RepId == RepId
                                               && p.OldState == RepState.Within15Miles.ToString()
                                               && p.NewState == RepState.OnSite.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAWithin15MilesRepWithActiveRequest_WhenArriveHandled_ThenSendRepArrivedIsInvokedWithRepIdAndRequestId()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRepArrivedAsync(
            $"requester:{RequesterId}",
            It.Is<RepArrivedPayload>(p => p.RepId == RepId && p.RequestId == RequestId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepWithNoActiveRequest_WhenArriveHandled_ThenNoActiveAssignedRequestExceptionIsThrownAndNoSideEffects()
    {
        // Arrange
        _serviceRequestRepository.Setup(r => r.GetActiveByRepIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NoActiveAssignedRequestException>();
        _serviceRequestRepository.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _repStateRepository.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(It.IsAny<string>(), It.IsAny<RepStateChangedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _requesterHub.Verify(h => h.SendRepArrivedAsync(It.IsAny<string>(), It.IsAny<RepArrivedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenARepNotWithin15Miles_WhenArriveHandled_ThenInvalidRepStateExceptionIsThrownAndRequestIsNotMutatedOrBroadcast()
    {
        // Arrange
        SetupHappyPath(repState: RepState.EnRoute);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidRepStateException>();
        _serviceRequestRepository.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _repStateRepository.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(It.IsAny<string>(), It.IsAny<RepStateChangedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _requesterHub.Verify(h => h.SendRepArrivedAsync(It.IsAny<string>(), It.IsAny<RepArrivedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
