using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.JobOffers.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.JobOffers;

public class AcceptJobOfferCommandHandlerTests
{
    private readonly Mock<IJobOfferRepository> _jobOfferRepository = new();
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepository = new();
    private readonly Mock<IRepStateRepository> _repStateRepository = new();
    private readonly Mock<IVehicleRepository> _vehicleRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRequesterHubService> _requesterHub = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();
    private readonly AcceptJobOfferCommandHandler _handler;

    private static readonly Guid OfferId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid RepId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid DealerId = Guid.NewGuid();

    public AcceptJobOfferCommandHandlerTests()
    {
        _handler = new AcceptJobOfferCommandHandler(
            _jobOfferRepository.Object,
            _serviceRequestRepository.Object,
            _repStateRepository.Object,
            _vehicleRepository.Object,
            _userRepository.Object,
            _requesterHub.Object,
            _dispatchHub.Object);
    }

    private static readonly Guid DisplacedFromRepId = Guid.NewGuid();

    private void SetupHappyPath(
        JobOfferStatus offerStatus = JobOfferStatus.Pending,
        ServiceRequestStatus requestStatus = ServiceRequestStatus.Pending,
        RepState repState = RepState.Available,
        bool withVehiclePosition = true,
        Guid? displacedFromRepId = null)
    {
        var offer = new JobOffer
        {
            Id = OfferId,
            ServiceRequestId = RequestId,
            RepId = RepId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = offerStatus
        };
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
            DisplacedFromRepId = displacedFromRepId,
            CreatedAt = DateTime.UtcNow
        };
        var repStateRecord = new RepStateRecord
        {
            RepId = RepId,
            State = repState,
            UpdatedAt = DateTime.UtcNow
        };

        _jobOfferRepository.Setup(r => r.GetByIdAsync(OfferId, It.IsAny<CancellationToken>())).ReturnsAsync(offer);
        _serviceRequestRepository.Setup(r => r.GetByIdAsync(RequestId, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repStateRepository.Setup(r => r.GetByRepIdAsync(RepId, It.IsAny<CancellationToken>())).ReturnsAsync(repStateRecord);
        _userRepository.Setup(r => r.FindByIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = RepId, Name = "Rep One" });
        _userRepository.Setup(r => r.FindByIdAsync(DisplacedFromRepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = DisplacedFromRepId, Name = "Rep Two" });

        if (withVehiclePosition)
        {
            _vehicleRepository.Setup(r => r.GetByClaimedRepIdAsync(RepId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Vehicle { Id = Guid.NewGuid(), ClaimedByRepId = RepId, Registration = "V-001", LastLatitude = 41.5, LastLongitude = -93.6 });
        }
        else
        {
            _vehicleRepository.Setup(r => r.GetByClaimedRepIdAsync(RepId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Vehicle?)null);
        }
    }

    private AcceptJobOfferCommand Command() => new(OfferId, RepId);

    [Fact]
    public async Task GivenAPendingOffer_WhenHandlerAccepts_ThenOfferIsPersistedAsAccepted()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _jobOfferRepository.Verify(r => r.UpdateAsync(
            It.Is<JobOffer>(o => o.Id == OfferId && o.Status == JobOfferStatus.Accepted),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAPendingOffer_WhenHandlerAccepts_ThenRequestIsPersistedAsAssignedToRep()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _serviceRequestRepository.Verify(r => r.UpdateAsync(
            It.Is<ServiceRequest>(s => s.Id == RequestId
                                       && s.Status == ServiceRequestStatus.Assigned
                                       && s.AssignedRepId == RepId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAPendingOffer_WhenHandlerAccepts_ThenRepStateIsUpsertedAsEnRoute()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _repStateRepository.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == RepId
                                       && s.State == RepState.EnRoute
                                       && s.ActiveRequestId == RequestId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAPendingOffer_WhenHandlerAccepts_ThenResultReflectsTransitionedStates()
    {
        // Arrange
        SetupHappyPath();

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.OfferId.Should().Be(OfferId);
        result.RequestId.Should().Be(RequestId);
        result.OfferStatus.Should().Be(JobOfferStatus.Accepted.ToString());
        result.RequestStatus.Should().Be(ServiceRequestStatus.Assigned.ToString());
        result.RepState.Should().Be(RepState.EnRoute.ToString());
    }

    [Fact]
    public async Task GivenAPendingOffer_WhenHandlerAccepts_ThenSendRepAssignedIsInvokedWithRepIdNameEtaAndPosition()
    {
        // Arrange
        SetupHappyPath();
        var expectedDistance = HaversineCalculator.DistanceMiles(41.5, -93.6, 41.6, -93.6);
        var expectedEta = HaversineCalculator.EtaMinutes(expectedDistance);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRepAssignedAsync(
            $"requester:{RequesterId}",
            It.Is<RepAssignedPayload>(p => p.RepId == RepId
                                           && p.RepName == "Rep One"
                                           && Math.Abs(p.EtaMinutes - expectedEta) < 0.0001
                                           && p.Latitude == 41.5
                                           && p.Longitude == -93.6),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepWithAClaimedVehicle_WhenOfferAccepted_ThenRepAssignedPayloadCarriesVehicleRegistration()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRepAssignedAsync(
            $"requester:{RequesterId}",
            It.Is<RepAssignedPayload>(p => p.VehicleRegistration == "V-001"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepWithNoClaimedVehicle_WhenOfferAccepted_ThenRepAssignedVehicleRegistrationIsEmptyString()
    {
        // Arrange
        SetupHappyPath(withVehiclePosition: false);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRepAssignedAsync(
            $"requester:{RequesterId}",
            It.Is<RepAssignedPayload>(p => p.VehicleRegistration == string.Empty),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAPendingOffer_WhenHandlerAccepts_ThenSendServiceRequestAssignedIsInvoked()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _dispatchHub.Verify(h => h.SendServiceRequestAssignedAsync(
            $"dealer:{DealerId}",
            It.Is<ServiceRequestAssignedPayload>(p => p.RequestId == RequestId
                                                      && p.RepId == RepId
                                                      && p.RepName == "Rep One"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAPendingOffer_WhenHandlerAccepts_ThenSendRepStateChangedIsInvokedWithOldAndNewState()
    {
        // Arrange
        SetupHappyPath(repState: RepState.Available);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(
            $"dealer:{DealerId}",
            It.Is<RepStateChangedPayload>(p => p.RepId == RepId
                                               && p.OldState == RepState.Available.ToString()
                                               && p.NewState == RepState.EnRoute.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(JobOfferStatus.Accepted)]
    [InlineData(JobOfferStatus.Declined)]
    [InlineData(JobOfferStatus.Expired)]
    public async Task GivenANonPendingOffer_WhenHandlerAccepts_ThenInvalidJobOfferStateExceptionIsThrownAndNoEventsFire(JobOfferStatus status)
    {
        // Arrange
        SetupHappyPath(offerStatus: status);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidJobOfferStateException>();
        _jobOfferRepository.Verify(r => r.UpdateAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Never);
        _serviceRequestRepository.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _repStateRepository.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _requesterHub.Verify(h => h.SendRepAssignedAsync(It.IsAny<string>(), It.IsAny<RepAssignedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendServiceRequestAssignedAsync(It.IsAny<string>(), It.IsAny<ServiceRequestAssignedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(It.IsAny<string>(), It.IsAny<RepStateChangedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenADisplacedRequest_WhenANewRepAcceptsIt_ThenRepRedirectedIsEmittedToDisplacedRequester()
    {
        // Arrange
        SetupHappyPath(displacedFromRepId: DisplacedFromRepId);
        var expectedDistance = HaversineCalculator.DistanceMiles(41.5, -93.6, 41.6, -93.6);
        var expectedEta = HaversineCalculator.EtaMinutes(expectedDistance);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRepRedirectedAsync(
            $"requester:{RequesterId}",
            It.Is<RepRedirectedPayload>(p => p.OldRepName == "Rep Two"
                                             && p.NewRepName == "Rep One"
                                             && Math.Abs(p.NewEtaMinutes - expectedEta) < 0.0001),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenADisplacedRequest_WhenANewRepAcceptsIt_ThenDisplacementIsCleared()
    {
        // Arrange
        SetupHappyPath(displacedFromRepId: DisplacedFromRepId);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _serviceRequestRepository.Verify(r => r.UpdateAsync(
            It.Is<ServiceRequest>(s => s.Id == RequestId && s.DisplacedFromRepId == null),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GivenANonDisplacedRequest_WhenAccepted_ThenNoRepRedirectedIsEmitted()
    {
        // Arrange
        SetupHappyPath(displacedFromRepId: null);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRepRedirectedAsync(
            It.IsAny<string>(), It.IsAny<RepRedirectedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenANonExistentOffer_WhenHandlerAccepts_ThenKeyNotFoundExceptionIsThrown()
    {
        // Arrange
        _jobOfferRepository.Setup(r => r.GetByIdAsync(OfferId, It.IsAny<CancellationToken>())).ReturnsAsync((JobOffer?)null);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
