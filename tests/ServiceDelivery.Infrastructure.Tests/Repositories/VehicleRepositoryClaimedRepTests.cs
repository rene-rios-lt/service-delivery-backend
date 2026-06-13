using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class VehicleRepositoryClaimedRepTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VehicleRepositoryClaimedRepTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenAVehicleClaimedByRep_WhenGetByClaimedRepId_ThenThatVehicleReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();

        context.Vehicles.AddRange(
            new Vehicle { Id = vehicleId, DealerId = dealerId, Registration = "V-001", ClaimedByRepId = repId, LastLatitude = 41.5, LastLongitude = -93.6 },
            new Vehicle { Id = Guid.NewGuid(), DealerId = dealerId, Registration = "V-002", ClaimedByRepId = Guid.NewGuid() },
            new Vehicle { Id = Guid.NewGuid(), DealerId = dealerId, Registration = "V-003", ClaimedByRepId = null });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetByClaimedRepIdAsync(repId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(vehicleId);
        result.LastLatitude.Should().Be(41.5);
        result.LastLongitude.Should().Be(-93.6);
    }

    [Fact]
    public async Task GivenNoVehicleClaimedByRep_WhenGetByClaimedRepId_ThenReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();

        context.Vehicles.Add(
            new Vehicle { Id = Guid.NewGuid(), DealerId = dealerId, Registration = "V-001", ClaimedByRepId = null });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetByClaimedRepIdAsync(repId);

        // Assert
        result.Should().BeNull();
    }
}
