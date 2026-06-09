using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Vehicles.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

public class GetAvailableVehiclesQueryHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepositoryMock;
    private readonly GetAvailableVehiclesQueryHandler _handler;

    public GetAvailableVehiclesQueryHandlerTests()
    {
        _vehicleRepositoryMock = new Mock<IVehicleRepository>();
        _handler = new GetAvailableVehiclesQueryHandler(_vehicleRepositoryMock.Object);
    }

    [Fact]
    public async Task GivenMixedClaimedAndUnclaimedVehicles_WhenGetAvailableVehiclesHandled_ThenOnlyUnclaimedAreReturned()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var unclaimedVehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            Registration = "V-001",
            ClaimedByRepId = null,
            Equipment = new List<VehicleEquipment>()
        };

        _vehicleRepositoryMock
            .Setup(r => r.GetUnclaimedByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle> { unclaimedVehicle });

        var query = new GetAvailableVehiclesQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].VehicleId.Should().Be(unclaimedVehicle.Id);
    }

    [Fact]
    public async Task GivenAllVehiclesClaimed_WhenGetAvailableVehiclesHandled_ThenReturnsEmptyList()
    {
        // Arrange
        var dealerId = Guid.NewGuid();

        _vehicleRepositoryMock
            .Setup(r => r.GetUnclaimedByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle>());

        var query = new GetAvailableVehiclesQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenGetAvailableVehiclesHandled_ThenDtoContainsVehicleIdRegistrationAndEquipment()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-007",
            ClaimedByRepId = null,
            Equipment = new List<VehicleEquipment>
            {
                new() { VehicleId = vehicleId, EquipmentType = EquipmentType.HydraulicTool },
                new() { VehicleId = vehicleId, EquipmentType = EquipmentType.ElectricalDiagnosticKit }
            }
        };

        _vehicleRepositoryMock
            .Setup(r => r.GetUnclaimedByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle> { vehicle });

        var query = new GetAvailableVehiclesQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.VehicleId.Should().Be(vehicleId);
        dto.Registration.Should().Be("V-007");
        dto.Equipment.Should().BeEquivalentTo(new[] { "HydraulicTool", "ElectricalDiagnosticKit" });
    }
}
