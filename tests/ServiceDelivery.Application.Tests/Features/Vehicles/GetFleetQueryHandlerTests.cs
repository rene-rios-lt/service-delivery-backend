using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Vehicles.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

public class GetFleetQueryHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepositoryMock;
    private readonly GetFleetQueryHandler _handler;

    public GetFleetQueryHandlerTests()
    {
        _vehicleRepositoryMock = new Mock<IVehicleRepository>();
        _handler = new GetFleetQueryHandler(_vehicleRepositoryMock.Object);
    }

    [Fact]
    public async Task GivenVehiclesExist_WhenGetFleetHandled_ThenDtoContainsAllRequiredFields()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-001",
            ClaimedByRepId = repId,
            Equipment = new List<VehicleEquipment>
            {
                new() { VehicleId = vehicleId, EquipmentType = EquipmentType.HydraulicTool },
                new() { VehicleId = vehicleId, EquipmentType = EquipmentType.ElectricalDiagnosticKit }
            }
        };

        _vehicleRepositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle> { vehicle });

        var query = new GetFleetQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.VehicleId.Should().Be(vehicleId);
        dto.Registration.Should().Be("V-001");
        dto.CurrentRepId.Should().Be(repId);
        dto.Equipment.Should().BeEquivalentTo(new[] { "HydraulicTool", "ElectricalDiagnosticKit" });
    }

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenGetFleetHandled_ThenStateIsUnclaimed()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            Registration = "V-001",
            ClaimedByRepId = null,
            Equipment = new List<VehicleEquipment>()
        };

        _vehicleRepositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle> { vehicle });

        var query = new GetFleetQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result[0].State.Should().Be("Unclaimed");
    }

    [Fact]
    public async Task GivenAClaimedVehicle_WhenGetFleetHandled_ThenStateIsClaimed()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            Registration = "V-002",
            ClaimedByRepId = Guid.NewGuid(),
            Equipment = new List<VehicleEquipment>()
        };

        _vehicleRepositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle> { vehicle });

        var query = new GetFleetQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result[0].State.Should().Be("Claimed");
    }

    [Fact]
    public async Task GivenVehicleWithNoPosition_WhenGetFleetHandled_ThenLastPositionIsNull()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            Registration = "V-003",
            ClaimedByRepId = null,
            LastLatitude = null,
            LastLongitude = null,
            LastPositionUpdatedAt = null,
            Equipment = new List<VehicleEquipment>()
        };

        _vehicleRepositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle> { vehicle });

        var query = new GetFleetQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result[0].LastPosition.Should().BeNull();
    }

    [Fact]
    public async Task GivenVehicleWithPosition_WhenGetFleetHandled_ThenLastPositionIsPopulated()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var updatedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            Registration = "V-004",
            ClaimedByRepId = null,
            LastLatitude = 51.5074,
            LastLongitude = -0.1278,
            LastPositionUpdatedAt = updatedAt,
            Equipment = new List<VehicleEquipment>()
        };

        _vehicleRepositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle> { vehicle });

        var query = new GetFleetQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var pos = result[0].LastPosition;
        pos.Should().NotBeNull();
        pos!.Lat.Should().Be(51.5074);
        pos.Lng.Should().Be(-0.1278);
        pos.UpdatedAt.Should().Be(updatedAt);
    }
}
