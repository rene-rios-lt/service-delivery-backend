using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Simulator.Queries;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Application.Tests.Features.Simulator;

public class GetFleetStateQueryHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepositoryMock;
    private readonly GetFleetStateQueryHandler _handler;

    public GetFleetStateQueryHandlerTests()
    {
        _vehicleRepositoryMock = new Mock<IVehicleRepository>();
        _handler = new GetFleetStateQueryHandler(_vehicleRepositoryMock.Object);
    }

    [Fact]
    public async Task GivenAClaimedRepWithActiveRequest_WhenGetFleetStateHandled_ThenDtoContainsAllRequiredFields()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var jobState = new List<FleetJobState>
        {
            new(vehicleId, repId, RepState.EnRoute, false, 41.5, -93.6),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetFleetJobStateByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobState);

        // Act
        var result = await _handler.Handle(new GetFleetStateQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.VehicleId.Should().Be(vehicleId);
        dto.ClaimingRepId.Should().Be(repId);
        dto.RepState.Should().Be("EnRoute");
        dto.HumanControlled.Should().BeFalse();
        dto.ActiveRequestLocation.Should().NotBeNull();
        dto.ActiveRequestLocation!.Lat.Should().Be(41.5);
        dto.ActiveRequestLocation.Lng.Should().Be(-93.6);
    }

    [Fact]
    public async Task GivenAClaimedRepWithNoActiveRequest_WhenGetFleetStateHandled_ThenActiveRequestLocationIsNull()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var jobState = new List<FleetJobState>
        {
            new(vehicleId, repId, RepState.Available, false, null, null),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetFleetJobStateByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobState);

        // Act
        var result = await _handler.Handle(new GetFleetStateQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].ClaimingRepId.Should().Be(repId);
        result[0].RepState.Should().Be("Available");
        result[0].ActiveRequestLocation.Should().BeNull();
    }

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenGetFleetStateHandled_ThenRepFieldsAreDefaultAndLocationNull()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var jobState = new List<FleetJobState>
        {
            new(vehicleId, null, null, false, null, null),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetFleetJobStateByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobState);

        // Act
        var result = await _handler.Handle(new GetFleetStateQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.VehicleId.Should().Be(vehicleId);
        dto.ClaimingRepId.Should().BeNull();
        dto.RepState.Should().Be("Offline");
        dto.HumanControlled.Should().BeFalse();
        dto.ActiveRequestLocation.Should().BeNull();
    }

    [Fact]
    public async Task GivenAHumanControlledRep_WhenGetFleetStateHandled_ThenHumanControlledIsTrue()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var jobState = new List<FleetJobState>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), RepState.OnSite, true, 40.0, -90.0),
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetFleetJobStateByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobState);

        // Act
        var result = await _handler.Handle(new GetFleetStateQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].HumanControlled.Should().BeTrue();
    }
}
