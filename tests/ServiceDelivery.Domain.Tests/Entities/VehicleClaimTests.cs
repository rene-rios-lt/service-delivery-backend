using FluentAssertions;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Tests.Entities;

public class VehicleClaimTests
{
    [Fact]
    public void GivenAVehicleEntity_WhenInspected_ThenRowVersionPropertyExists()
    {
        // Arrange
        var vehicle = new Vehicle();

        // Act
        var rowVersion = vehicle.RowVersion;

        // Assert
        rowVersion.Should().NotBeNull();
    }
}
