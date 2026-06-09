using FluentAssertions;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Tests.Entities;

public class VehicleEntityTests
{
    [Fact]
    public void GivenANewVehicle_WhenConstructed_ThenClaimedByRepIdIsNull()
    {
        // Arrange
        var vehicle = new Vehicle();

        // Act
        var claimedByRepId = vehicle.ClaimedByRepId;

        // Assert
        claimedByRepId.Should().BeNull();
    }

    [Fact]
    public void GivenANewVehicle_WhenConstructed_ThenClaimedAtIsNull()
    {
        // Arrange
        var vehicle = new Vehicle();

        // Act
        var claimedAt = vehicle.ClaimedAt;

        // Assert
        claimedAt.Should().BeNull();
    }

    [Fact]
    public void GivenANewVehicle_WhenConstructed_ThenEquipmentCollectionIsEmpty()
    {
        // Arrange
        var vehicle = new Vehicle();

        // Act
        var equipment = vehicle.Equipment;

        // Assert
        equipment.Should().BeEmpty();
    }

    [Fact]
    public void GivenANewVehicle_WhenRegistrationSet_ThenRegistrationIsStored()
    {
        // Arrange
        var vehicle = new Vehicle();

        // Act
        vehicle.Registration = "V-001";

        // Assert
        vehicle.Registration.Should().Be("V-001");
    }
}
