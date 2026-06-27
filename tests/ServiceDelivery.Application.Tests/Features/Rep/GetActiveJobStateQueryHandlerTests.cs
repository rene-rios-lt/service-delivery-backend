using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Features.Rep.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Rep;

public class GetActiveJobStateQueryHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IDiagnosticTroubleCodeRepository> _dtcRepository = new();
    private readonly Mock<IVehicleRepository> _vehicleRepository = new();
    private readonly Mock<IRepStateRepository> _repStateRepository = new();
    private readonly GetActiveJobStateQueryHandler _handler;

    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid RepId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid DtcId = Guid.NewGuid();
    private static readonly Guid DealerId = Guid.NewGuid();

    private const double RequesterLat = 41.6;
    private const double RequesterLng = -93.6;
    private const double RepLat = 41.7;
    private const double RepLng = -93.7;

    public GetActiveJobStateQueryHandlerTests()
    {
        _handler = new GetActiveJobStateQueryHandler(
            _serviceRequestRepository.Object,
            _userRepository.Object,
            _dtcRepository.Object,
            _vehicleRepository.Object,
            _repStateRepository.Object);
    }

    private void SetupHappyPath(RepState repState = RepState.EnRoute)
    {
        var request = new ServiceRequest
        {
            Id = RequestId,
            DealerId = DealerId,
            RequesterId = RequesterId,
            DtcId = DtcId,
            Latitude = RequesterLat,
            Longitude = RequesterLng,
            Status = ServiceRequestStatus.Assigned,
            Tier = ServiceTier.Gold,
            AssignedRepId = RepId,
            CreatedAt = DateTime.UtcNow
        };
        var requester = new User { Id = RequesterId, Name = "Gold User 1", Role = UserRole.Requester, Tier = ServiceTier.Gold };
        var dtc = new DiagnosticTroubleCode { Id = DtcId, HumanReadableTitle = "Hydraulic system fault" };
        var vehicle = new Vehicle { Id = Guid.NewGuid(), ClaimedByRepId = RepId, LastLatitude = RepLat, LastLongitude = RepLng };
        var repStateRecord = new RepStateRecord { RepId = RepId, State = repState, ActiveRequestId = RequestId, UpdatedAt = DateTime.UtcNow };

        _serviceRequestRepository.Setup(r => r.GetActiveByRepIdAsync(RepId, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _userRepository.Setup(r => r.FindByIdAsync(RequesterId, It.IsAny<CancellationToken>())).ReturnsAsync(requester);
        _dtcRepository.Setup(r => r.GetByIdAsync(DtcId, It.IsAny<CancellationToken>())).ReturnsAsync(dtc);
        _vehicleRepository.Setup(r => r.GetByClaimedRepIdAsync(RepId, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);
        _repStateRepository.Setup(r => r.GetByRepIdAsync(RepId, It.IsAny<CancellationToken>())).ReturnsAsync(repStateRecord);
    }

    private GetActiveJobStateQuery Query() => new(RepId);

    [Fact]
    public async Task GivenARepWithAnAssignedRequest_WhenGetActiveJobStateHandled_ThenAllFieldsArePopulated()
    {
        // Arrange
        SetupHappyPath();

        // Act
        var result = await _handler.Handle(Query(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(RequestId);
        result.RequesterName.Should().Be("Gold User 1");
        result.DtcTitle.Should().Be("Hydraulic system fault");
        result.RequesterLat.Should().Be(RequesterLat);
        result.RequesterLng.Should().Be(RequesterLng);
        result.RepLat.Should().Be(RepLat);
        result.RepLng.Should().Be(RepLng);
        result.RepState.Should().Be(RepState.EnRoute.ToString());
    }

    [Fact]
    public async Task GivenARepEnRoute_WhenGetActiveJobStateHandled_ThenDistanceMilesEqualsHaversineDistance()
    {
        // Arrange
        SetupHappyPath(repState: RepState.EnRoute);
        var expectedDistance = HaversineCalculator.DistanceMiles(RepLat, RepLng, RequesterLat, RequesterLng);

        // Act
        var result = await _handler.Handle(Query(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DistanceMiles.Should().BeApproximately(expectedDistance, 0.0001);
    }

    [Fact]
    public async Task GivenARepOnSite_WhenGetActiveJobStateHandled_ThenDistanceMilesIsZero()
    {
        // Arrange
        SetupHappyPath(repState: RepState.OnSite);

        // Act
        var result = await _handler.Handle(Query(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DistanceMiles.Should().Be(0.0);
    }

    [Fact]
    public async Task GivenAGoldTierRequest_WhenGetActiveJobStateHandled_ThenTierIsMappedFromServiceRequest()
    {
        // Arrange
        SetupHappyPath();

        // Act
        var result = await _handler.Handle(Query(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Tier.Should().Be(ServiceTier.Gold.ToString());
    }

    [Fact]
    public async Task GivenARepEnRoute_WhenGetActiveJobStateHandled_ThenEtaMinutesIsCalculatedFromDistance()
    {
        // Arrange
        SetupHappyPath(repState: RepState.EnRoute);
        var expectedEta = (int)Math.Round(HaversineCalculator.EtaMinutes(
            HaversineCalculator.DistanceMiles(RepLat, RepLng, RequesterLat, RequesterLng)));

        // Act
        var result = await _handler.Handle(Query(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.EtaMinutes.Should().Be(expectedEta);
    }

    [Fact]
    public async Task GivenARepOnSite_WhenGetActiveJobStateHandled_ThenEtaMinutesIsZero()
    {
        // Arrange
        SetupHappyPath(repState: RepState.OnSite);

        // Act
        var result = await _handler.Handle(Query(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.EtaMinutes.Should().Be(0);
    }

    [Fact]
    public async Task GivenARepWithNoActiveRequest_WhenGetActiveJobStateHandled_ThenReturnsNull()
    {
        // Arrange
        _serviceRequestRepository.Setup(r => r.GetActiveByRepIdAsync(RepId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        // Act
        var result = await _handler.Handle(Query(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenARepWithAnAssignedRequest_WhenGetActiveJobStateHandled_ThenNoRepositoryWriteMethodsAreCalled()
    {
        // Arrange
        SetupHappyPath();

        // Act
        await _handler.Handle(Query(), CancellationToken.None);

        // Assert
        _serviceRequestRepository.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _serviceRequestRepository.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _vehicleRepository.Verify(r => r.UpdateAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
        _repStateRepository.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
