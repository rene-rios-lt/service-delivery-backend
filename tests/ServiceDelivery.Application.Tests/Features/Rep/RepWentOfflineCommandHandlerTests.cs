using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Rep.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Rep;

public class RepWentOfflineCommandHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepository = new();
    private readonly Mock<IRepStateRepository> _repStateRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IDiagnosticTroubleCodeRepository> _dtcRepository = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();
    private readonly Mock<IRequesterHubService> _requesterHub = new();
    private readonly Mock<IMatchingService> _matchingService = new();
    private readonly RepWentOfflineCommandHandler _handler;

    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid RepId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid DtcId = Guid.NewGuid();

    public RepWentOfflineCommandHandlerTests()
    {
        _handler = new RepWentOfflineCommandHandler(
            _serviceRequestRepository.Object,
            _repStateRepository.Object,
            _userRepository.Object,
            _dtcRepository.Object,
            _dispatchHub.Object,
            _requesterHub.Object,
            _matchingService.Object);
    }

    private void SetupHappyPath(
        ServiceRequestStatus requestStatus = ServiceRequestStatus.Assigned,
        RepState repState = RepState.EnRoute,
        bool humanControlled = false)
    {
        var request = new ServiceRequest
        {
            Id = RequestId,
            DealerId = DealerId,
            RequesterId = RequesterId,
            DtcId = DtcId,
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
            HumanControlled = humanControlled,
            UpdatedAt = DateTime.UtcNow
        };

        _serviceRequestRepository.Setup(r => r.GetActiveByRepIdAsync(RepId, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repStateRepository.Setup(r => r.GetByRepIdAsync(RepId, It.IsAny<CancellationToken>())).ReturnsAsync(repStateRecord);
        _userRepository.Setup(r => r.FindByIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = RepId, Name = "Rep One", DealerId = DealerId, Role = UserRole.ServiceRep });
        _dtcRepository.Setup(r => r.GetByIdAsync(DtcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticTroubleCode { Id = DtcId, DealerId = DealerId, Code = "DTC-001", HumanReadableTitle = "Hydraulic system fault" });
    }

    private RepWentOfflineCommand Command() => new(RepId, DealerId);

    [Fact]
    public async Task GivenARepWithAnActiveJob_WhenWentOfflineHandled_ThenRepStateIsOffline()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _repStateRepository.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == RepId
                                       && s.State == RepState.Offline
                                       && s.ActiveRequestId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepWithAnAssignedJob_WhenWentOfflineHandled_ThenRequestReturnsToPending()
    {
        // Arrange
        SetupHappyPath(requestStatus: ServiceRequestStatus.Assigned);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _serviceRequestRepository.Verify(r => r.UpdateAsync(
            It.Is<ServiceRequest>(s => s.Id == RequestId
                                       && s.Status == ServiceRequestStatus.Pending
                                       && s.AssignedRepId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepOffline_WhenHandled_ThenMatchingIsReRunForTheRequest()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _matchingService.Verify(m => m.RunAsync(RequestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAHumanControlledRep_WhenWentOfflineHandled_ThenHumanControlledMarkerIsCleared()
    {
        // Arrange
        SetupHappyPath(humanControlled: true);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _repStateRepository.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == RepId && s.HumanControlled == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-3 "vehicle stays Claimed" is covered behaviourally by the integration test
    // ParkedVehicleStaysClaimedTests (Api.Tests): the handler has no IVehicleRepository
    // collaborator, so a unit-level "claim not released" assertion can only exercise the test's
    // own mock and cannot fail on handler behaviour. The integration test runs the real handler
    // against real persistence and re-reads the vehicle row — that is the genuine guard.

    [Fact]
    public async Task GivenARepWithAnActiveJob_WhenWentOfflineHandled_ThenDispatchersReceiveRepOfflineMidJobWithRepNameAndDtcTitle()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _dispatchHub.Verify(h => h.SendRepOfflineMidJobAsync(
            $"dealer:{DealerId}",
            It.Is<RepOfflineMidJobPayload>(p => p.RepId == RepId
                                                && p.RequestId == RequestId
                                                && p.RepName == "Rep One"
                                                && p.DtcTitle == "Hydraulic system fault"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepWithAnActiveJob_WhenWentOfflineHandled_ThenRequesterReceivesRequestBackToPending()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRequestBackToPendingAsync(
            $"requester:{RequesterId}",
            It.Is<RequestBackToPendingPayload>(p => p.RequestId == RequestId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepWithNoActiveJob_WhenWentOfflineHandled_ThenNoReQueueOrBroadcastOccurs()
    {
        // Arrange
        _serviceRequestRepository.Setup(r => r.GetActiveByRepIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _serviceRequestRepository.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _repStateRepository.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendRepOfflineMidJobAsync(It.IsAny<string>(), It.IsAny<RepOfflineMidJobPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _requesterHub.Verify(h => h.SendRequestBackToPendingAsync(It.IsAny<string>(), It.IsAny<RequestBackToPendingPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _matchingService.Verify(m => m.RunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
