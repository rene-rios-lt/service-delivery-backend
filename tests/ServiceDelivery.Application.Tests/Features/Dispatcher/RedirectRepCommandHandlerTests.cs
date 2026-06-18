using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Dispatcher.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Dispatcher;

public class RedirectRepCommandHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepository = new();
    private readonly Mock<IRepStateRepository> _repStateRepository = new();
    private readonly Mock<IVehicleRepository> _vehicleRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IDiagnosticTroubleCodeRepository> _dtcRepository = new();
    private readonly Mock<IMatchingService> _matchingService = new();
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IRequesterHubService> _requesterHub = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();
    private readonly RedirectRepCommandHandler _handler;

    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid RepId = Guid.NewGuid();
    private static readonly Guid FromRequestId = Guid.NewGuid();
    private static readonly Guid ToRequestId = Guid.NewGuid();
    private static readonly Guid FromRequesterId = Guid.NewGuid();
    private static readonly Guid ToRequesterId = Guid.NewGuid();
    private static readonly Guid ToDtcId = Guid.NewGuid();

    // Current (displaced) request location. Vehicle is placed far from this to pass the proximity guard.
    private const double FromLat = 41.6;
    private const double FromLng = -93.6;
    private const double ToLat = 42.0;
    private const double ToLng = -94.0;
    private const double FarVehicleLat = 40.0;   // ~110 miles from the displaced request
    private const double FarVehicleLng = -93.6;
    private const double NearVehicleLat = 41.61;  // < 1 mile from the displaced request
    private const double NearVehicleLng = -93.6;

    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public RedirectRepCommandHandlerTests()
    {
        _handler = new RedirectRepCommandHandler(
            _serviceRequestRepository.Object,
            _repStateRepository.Object,
            _vehicleRepository.Object,
            _userRepository.Object,
            _dtcRepository.Object,
            _matchingService.Object,
            _repHub.Object,
            _requesterHub.Object,
            _dispatchHub.Object,
            new RedirectOptions { CooldownMinutes = 5 },
            () => Now);
    }

    private void Setup(
        RepState repState = RepState.EnRoute,
        ServiceTier fromTier = ServiceTier.Bronze,
        ServiceTier toTier = ServiceTier.Gold,
        DateTime? lastRedirectedAt = null,
        double vehicleLat = FarVehicleLat,
        double vehicleLng = FarVehicleLng,
        bool hasActiveRequest = true)
    {
        var fromRequest = new ServiceRequest
        {
            Id = FromRequestId,
            DealerId = DealerId,
            RequesterId = FromRequesterId,
            DtcId = Guid.NewGuid(),
            Latitude = FromLat,
            Longitude = FromLng,
            Status = ServiceRequestStatus.Assigned,
            Tier = fromTier,
            AssignedRepId = RepId,
            CreatedAt = Now.AddHours(-1)
        };
        var toRequest = new ServiceRequest
        {
            Id = ToRequestId,
            DealerId = DealerId,
            RequesterId = ToRequesterId,
            DtcId = ToDtcId,
            Latitude = ToLat,
            Longitude = ToLng,
            Status = ServiceRequestStatus.Pending,
            Tier = toTier,
            CreatedAt = Now.AddMinutes(-5)
        };
        var repStateRecord = new RepStateRecord
        {
            RepId = RepId,
            State = repState,
            ActiveRequestId = repState == RepState.EnRoute ? FromRequestId : null,
            LastRedirectedAt = lastRedirectedAt,
            UpdatedAt = Now.AddMinutes(-10)
        };

        _serviceRequestRepository.Setup(r => r.GetActiveByRepIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasActiveRequest ? fromRequest : null);
        _serviceRequestRepository.Setup(r => r.GetByIdAsync(ToRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toRequest);
        _serviceRequestRepository.Setup(r => r.GetByIdAsync(FromRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fromRequest);
        _repStateRepository.Setup(r => r.GetByRepIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repStateRecord);
        _vehicleRepository.Setup(r => r.GetByClaimedRepIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Vehicle { Id = Guid.NewGuid(), ClaimedByRepId = RepId, LastLatitude = vehicleLat, LastLongitude = vehicleLng });
        _userRepository.Setup(r => r.FindByIdAsync(ToRequesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = ToRequesterId, Name = "Gold Requester", Tier = toTier });
        _userRepository.Setup(r => r.FindByIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = RepId, Name = "Rep One" });
        _dtcRepository.Setup(r => r.GetByIdAsync(ToDtcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticTroubleCode { Id = ToDtcId, HumanReadableTitle = "Engine Fault" });
    }

    private RedirectRepCommand Command() => new(DealerId, RepId, ToRequestId);

    [Theory]
    [InlineData(RepState.Available)]
    [InlineData(RepState.Within15Miles)]
    [InlineData(RepState.Offline)]
    public async Task GivenARepNotEnRoute_WhenRedirectRequested_ThenRedirectNotAllowedWithReasonRepNotEnRoute(RepState state)
    {
        // Arrange
        Setup(repState: state, hasActiveRequest: state == RepState.Within15Miles);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<RedirectNotAllowedException>();
        ex.Which.Reason.Should().Be("RepNotEnRoute");
    }

    [Fact]
    public async Task GivenAnEqualOrLowerTier_WhenRedirectRequested_ThenRedirectNotAllowedWithReasonTierNotHigher()
    {
        // Arrange
        Setup(fromTier: ServiceTier.Silver, toTier: ServiceTier.Silver);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<RedirectNotAllowedException>();
        ex.Which.Reason.Should().Be("TierNotHigher");
    }

    [Fact]
    public async Task GivenAHigherTier_WhenRedirectRequested_ThenSucceeds()
    {
        // Arrange
        Setup(fromTier: ServiceTier.Bronze, toTier: ServiceTier.Silver);

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.ToRequestId.Should().Be(ToRequestId);
    }

    [Fact]
    public async Task GivenAGoldRequestDuringCooldown_WhenRedirectRequested_ThenSucceeds()
    {
        // Arrange
        Setup(fromTier: ServiceTier.Silver, toTier: ServiceTier.Gold, lastRedirectedAt: Now.AddMinutes(-1));

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.ToRequestId.Should().Be(ToRequestId);
    }

    [Fact]
    public async Task GivenARepWithinFifteenMilesOfCurrentRequester_WhenGoldRedirectRequested_ThenRedirectNotAllowedWithReasonWithinFifteenMiles()
    {
        // Arrange
        Setup(fromTier: ServiceTier.Silver, toTier: ServiceTier.Gold, vehicleLat: NearVehicleLat, vehicleLng: NearVehicleLng);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<RedirectNotAllowedException>();
        ex.Which.Reason.Should().Be("WithinFifteenMiles");
    }

    [Fact]
    public async Task GivenAnOnSiteRep_WhenGoldRedirectRequested_ThenRedirectNotAllowedWithReasonRepOnSite()
    {
        // Arrange
        Setup(repState: RepState.OnSite, fromTier: ServiceTier.Silver, toTier: ServiceTier.Gold);
        // OnSite rep still bears an active request.
        _repStateRepository.Setup(r => r.GetByRepIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepStateRecord { RepId = RepId, State = RepState.OnSite, ActiveRequestId = FromRequestId, UpdatedAt = Now });

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<RedirectNotAllowedException>();
        ex.Which.Reason.Should().Be("RepOnSite");
    }

    [Fact]
    public async Task GivenASilverRequestDuringCooldown_WhenRedirectRequested_ThenRedirectNotAllowedWithReasonCooldownActive()
    {
        // Arrange
        Setup(fromTier: ServiceTier.Bronze, toTier: ServiceTier.Silver, lastRedirectedAt: Now.AddMinutes(-2));

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<RedirectNotAllowedException>();
        ex.Which.Reason.Should().Be("CooldownActive");
    }

    [Fact]
    public async Task GivenASilverRequestAfterCooldownElapsed_WhenRedirectRequested_ThenSucceeds()
    {
        // Arrange
        Setup(fromTier: ServiceTier.Bronze, toTier: ServiceTier.Silver, lastRedirectedAt: Now.AddMinutes(-6));

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.ToRequestId.Should().Be(ToRequestId);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenRequested_ThenDisplacedRequestIsPendingAndUnassigned()
    {
        // Arrange
        Setup();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _serviceRequestRepository.Verify(r => r.UpdateAsync(
            It.Is<ServiceRequest>(s => s.Id == FromRequestId
                                       && s.Status == ServiceRequestStatus.Pending
                                       && s.AssignedRepId == null
                                       && s.DisplacedFromRepId == RepId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenRequested_ThenRepIsEnRouteToNewRequest()
    {
        // Arrange
        Setup();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _repStateRepository.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == RepId
                                       && s.State == RepState.EnRoute
                                       && s.ActiveRequestId == ToRequestId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenRequested_ThenRepLastRedirectedAtIsStamped()
    {
        // Arrange
        Setup();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _repStateRepository.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == RepId && s.LastRedirectedAt == Now),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenRequested_ThenMatchingRunsForDisplacedRequest()
    {
        // Arrange
        Setup();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _matchingService.Verify(m => m.RunAsync(FromRequestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenRequested_ThenRedirectReceivedSentToRep()
    {
        // Arrange
        Setup();
        var expectedDistance = HaversineCalculator.DistanceMiles(FarVehicleLat, FarVehicleLng, ToLat, ToLng);
        var expectedEta = HaversineCalculator.EtaMinutes(expectedDistance);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _repHub.Verify(h => h.SendRedirectReceivedAsync(
            $"rep:{RepId}",
            It.Is<RedirectReceivedPayload>(p => p.NewRequestId == ToRequestId
                                                && p.RequesterName == "Gold Requester"
                                                && p.RequesterTier == ServiceTier.Gold.ToString()
                                                && p.DtcTitle == "Engine Fault"
                                                && p.Latitude == ToLat
                                                && p.Longitude == ToLng
                                                && Math.Abs(p.DistanceMiles - expectedDistance) < 0.0001
                                                && Math.Abs(p.EtaMinutes - expectedEta) < 0.0001),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenRequested_ThenRepAssignedSentToNewRequester()
    {
        // Arrange
        Setup();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRepAssignedAsync(
            $"requester:{ToRequesterId}",
            It.Is<RepAssignedPayload>(p => p.RepId == RepId && p.RepName == "Rep One"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenRequested_ThenRepStateChangedSentToDispatchers()
    {
        // Arrange
        Setup();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _dispatchHub.Verify(h => h.SendRepStateChangedAsync(
            $"dealer:{DealerId}",
            It.Is<RepStateChangedPayload>(p => p.RepId == RepId
                                               && p.OldState == RepState.EnRoute.ToString()
                                               && p.NewState == RepState.EnRoute.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenRequested_ThenRepRedirectedIsNotSentSynchronously()
    {
        // Arrange
        Setup();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _requesterHub.Verify(h => h.SendRepRedirectedAsync(
            It.IsAny<string>(), It.IsAny<RepRedirectedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenARepWithNoActiveRequest_WhenRedirectRequested_ThenRedirectNotAllowedWithReasonRepNotEnRoute()
    {
        // Arrange
        Setup(hasActiveRequest: false);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<RedirectNotAllowedException>();
        ex.Which.Reason.Should().Be("RepNotEnRoute");
    }

    [Fact]
    public async Task GivenAnUnknownTargetRequest_WhenRedirectRequested_ThenKeyNotFoundExceptionIsThrown()
    {
        // Arrange
        Setup();
        _serviceRequestRepository.Setup(r => r.GetByIdAsync(ToRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
