using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Dispatcher.Queries;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Application.Tests.Features.Dispatcher;

public class GetDispatcherFleetQueryHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepositoryMock;
    private readonly GetDispatcherFleetQueryHandler _handler;

    public GetDispatcherFleetQueryHandlerTests()
    {
        _vehicleRepositoryMock = new Mock<IVehicleRepository>();
        _handler = new GetDispatcherFleetQueryHandler(_vehicleRepositoryMock.Object);
    }

    [Fact]
    public async Task GivenVehiclesAcrossTwoDealers_WhenGetDispatcherFleetHandled_ThenOnlyOwnDealerEntriesReturned()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var ownVehicleId = Guid.NewGuid();
        var entries = new List<DispatcherFleetEntry>
        {
            new(ownVehicleId, "V-001", null, null, null, false, null, null, null, null),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetDispatcherFleetByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(new GetDispatcherFleetQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].VehicleId.Should().Be(ownVehicleId);
        _vehicleRepositoryMock.Verify(
            r => r.GetDispatcherFleetByDealerAsync(dealerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAClaimedRepWithActiveRequest_WhenGetDispatcherFleetHandled_ThenDtoContainsAllRequiredFields()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var entries = new List<DispatcherFleetEntry>
        {
            new(vehicleId, "V-001", repId, "Riley Rep", RepState.EnRoute, false, 41.5, -93.6, requestId, ServiceTier.Gold),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetDispatcherFleetByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(new GetDispatcherFleetQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.RepId.Should().Be(repId);
        dto.Name.Should().Be("Riley Rep");
        dto.State.Should().Be("EnRoute");
        dto.VehicleId.Should().Be(vehicleId);
        dto.Registration.Should().Be("V-001");
        dto.LastPosition.Should().NotBeNull();
        dto.LastPosition!.Lat.Should().Be(41.5);
        dto.LastPosition.Lng.Should().Be(-93.6);
        dto.ActiveRequestId.Should().Be(requestId);
        dto.ActiveRequestTier.Should().Be("Gold");
    }

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenGetDispatcherFleetHandled_ThenRepFieldsNullAndStateOffline()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var entries = new List<DispatcherFleetEntry>
        {
            new(vehicleId, "V-002", null, null, null, false, 10.0, 20.0, null, null),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetDispatcherFleetByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(new GetDispatcherFleetQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.RepId.Should().Be(Guid.Empty);
        dto.Name.Should().BeNull();
        dto.State.Should().Be("Offline");
        dto.ActiveRequestId.Should().BeNull();
        dto.ActiveRequestTier.Should().BeNull();
        dto.Registration.Should().Be("V-002");
    }

    [Fact]
    public async Task GivenARepWhoseVehicleHasNoPosition_WhenGetDispatcherFleetHandled_ThenLastPositionIsNull()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var entries = new List<DispatcherFleetEntry>
        {
            new(Guid.NewGuid(), "V-003", Guid.NewGuid(), "Riley Rep", RepState.Available, false, null, null, null, null),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetDispatcherFleetByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(new GetDispatcherFleetQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].LastPosition.Should().BeNull();
    }

    [Fact]
    public async Task GivenAClaimedIdleRep_WhenGetDispatcherFleetHandled_ThenActiveRequestFieldsAreNull()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var entries = new List<DispatcherFleetEntry>
        {
            new(Guid.NewGuid(), "V-004", Guid.NewGuid(), "Riley Rep", RepState.Available, false, 1.0, 2.0, null, null),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetDispatcherFleetByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(new GetDispatcherFleetQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].ActiveRequestId.Should().BeNull();
        result[0].ActiveRequestTier.Should().BeNull();
    }

    [Fact]
    public async Task GivenAHumanControlledRep_WhenGetDispatcherFleetHandled_ThenHumanControlledIsTrue()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var entries = new List<DispatcherFleetEntry>
        {
            new(Guid.NewGuid(), "V-005", Guid.NewGuid(), "Riley Rep", RepState.OnSite, true, 1.0, 2.0, Guid.NewGuid(), ServiceTier.Silver),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetDispatcherFleetByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(new GetDispatcherFleetQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].HumanControlled.Should().BeTrue();
    }
}
