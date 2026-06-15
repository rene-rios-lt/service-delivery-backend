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

public class CompleteCommandHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepository = new();
    private readonly Mock<IRepStateRepository> _repStateRepository = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();
    private readonly Mock<IRequesterHubService> _requesterHub = new();
    private readonly Mock<IMatchingService> _matchingService = new();
    private readonly CompleteCommandHandler _handler;

    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid RepId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid DealerId = Guid.NewGuid();

    public CompleteCommandHandlerTests()
    {
        _handler = new CompleteCommandHandler(
            _serviceRequestRepository.Object,
            _repStateRepository.Object,
            _dispatchHub.Object,
            _requesterHub.Object,
            _matchingService.Object);
    }

    private void SetupHappyPath(
        ServiceRequestStatus requestStatus = ServiceRequestStatus.InProgress,
        RepState repState = RepState.OnSite)
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

    private CompleteCommand Command() => new(RepId);

    [Fact]
    public async Task GivenAnOnSiteRepWithInProgressRequest_WhenCompleteHandled_ThenRepStateIsUpsertedAsAvailable()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _repStateRepository.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == RepId
                                       && s.State == RepState.Available
                                       && s.ActiveRequestId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOnSiteRepWithInProgressRequest_WhenCompleteHandled_ThenRequestIsPersistedAsCompleted()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _serviceRequestRepository.Verify(r => r.UpdateAsync(
            It.Is<ServiceRequest>(s => s.Id == RequestId && s.Status == ServiceRequestStatus.Completed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOnSiteRepWithInProgressRequest_WhenCompleteHandled_ThenNoVehicleRepositoryMutationOccurs()
    {
        // Arrange
        // The handler is constructed with no IVehicleRepository — the vehicle stays Claimed
        // because completion never touches vehicle ownership. This test guards that the
        // handler's collaborator set contains no vehicle repository (compile-time) and that
        // completing a job mutates only the rep-state and service-request repositories.
        SetupHappyPath();
        var handlerType = typeof(CompleteCommandHandler);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        handlerType.GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType.Name)
            .Should().NotContain("IVehicleRepository");

        _serviceRequestRepository.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _repStateRepository.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOnSiteRepWithInProgressRequest_WhenCompleteHandled_ThenSendServiceCompletedIsInvokedForRequester()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendServiceCompletedAsync(
            $"requester:{RequesterId}",
            It.Is<ServiceCompletedPayload>(p => p.RequestId == RequestId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOnSiteRepWithInProgressRequest_WhenCompleteHandled_ThenSendRepStateChangedIsInvokedWithOnSiteToAvailable()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(
            $"dealer:{DealerId}",
            It.Is<RepStateChangedPayload>(p => p.RepId == RepId
                                               && p.OldState == RepState.OnSite.ToString()
                                               && p.NewState == RepState.Available.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOnSiteRepWithInProgressRequest_WhenCompleteHandled_ThenSendServiceRequestCompletedIsInvokedWithRequestId()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _dispatchHub.Verify(h => h.SendServiceRequestCompletedAsync(
            $"dealer:{DealerId}",
            It.Is<ServiceRequestCompletedPayload>(p => p.RequestId == RequestId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnOnSiteRepWithInProgressRequest_WhenCompleteHandled_ThenMatchingIsReRunForTheDealer()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _matchingService.Verify(m => m.RunForPendingByDealerAsync(DealerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepWithNoActiveRequest_WhenCompleteHandled_ThenNoActiveAssignedRequestExceptionIsThrownAndNoSideEffects()
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
        _requesterHub.Verify(h => h.SendServiceCompletedAsync(It.IsAny<string>(), It.IsAny<ServiceCompletedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(It.IsAny<string>(), It.IsAny<RepStateChangedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendServiceRequestCompletedAsync(It.IsAny<string>(), It.IsAny<ServiceRequestCompletedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _matchingService.Verify(m => m.RunForPendingByDealerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenARepWhoseRequestIsNotInProgress_WhenCompleteHandled_ThenInvalidServiceRequestStateExceptionIsThrownAndNoSideEffects()
    {
        // Arrange
        SetupHappyPath(requestStatus: ServiceRequestStatus.Assigned);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidServiceRequestStateException>();
        _serviceRequestRepository.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _repStateRepository.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _requesterHub.Verify(h => h.SendServiceCompletedAsync(It.IsAny<string>(), It.IsAny<ServiceCompletedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(It.IsAny<string>(), It.IsAny<RepStateChangedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendServiceRequestCompletedAsync(It.IsAny<string>(), It.IsAny<ServiceRequestCompletedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _matchingService.Verify(m => m.RunForPendingByDealerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
