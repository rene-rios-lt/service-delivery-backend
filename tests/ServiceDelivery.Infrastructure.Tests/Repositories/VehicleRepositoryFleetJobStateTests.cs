using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class VehicleRepositoryFleetJobStateTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VehicleRepositoryFleetJobStateTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenAVehicleClaimedByARepOnAnActiveRequest_WhenGetFleetJobStateByDealer_ThenLocationMatchesRequest()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle { Id = vehicleId, DealerId = dealerId, Registration = "V-001", ClaimedByRepId = repId });
        context.RepStateRecords.Add(new RepStateRecord { RepId = repId, State = RepState.EnRoute, HumanControlled = true, ActiveRequestId = requestId });
        context.ServiceRequests.Add(new ServiceRequest { Id = requestId, DealerId = dealerId, Latitude = 41.5, Longitude = -93.6, Status = ServiceRequestStatus.Assigned, AssignedRepId = repId });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context, new RedirectOptions());

        // Act
        var result = await repository.GetFleetJobStateByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        var row = result[0];
        row.VehicleId.Should().Be(vehicleId);
        row.ClaimingRepId.Should().Be(repId);
        row.RepState.Should().Be(RepState.EnRoute);
        row.HumanControlled.Should().BeTrue();
        row.ActiveRequestLatitude.Should().Be(41.5);
        row.ActiveRequestLongitude.Should().Be(-93.6);
    }

    [Fact]
    public async Task GivenAVehicleClaimedByAnIdleRep_WhenGetFleetJobStateByDealer_ThenLocationIsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle { Id = vehicleId, DealerId = dealerId, Registration = "V-001", ClaimedByRepId = repId });
        context.RepStateRecords.Add(new RepStateRecord { RepId = repId, State = RepState.Available, HumanControlled = false, ActiveRequestId = null });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context, new RedirectOptions());

        // Act
        var result = await repository.GetFleetJobStateByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        var row = result[0];
        row.VehicleId.Should().Be(vehicleId);
        row.ClaimingRepId.Should().Be(repId);
        row.RepState.Should().Be(RepState.Available);
        row.HumanControlled.Should().BeFalse();
        row.ActiveRequestLatitude.Should().BeNull();
        row.ActiveRequestLongitude.Should().BeNull();
    }

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenGetFleetJobStateByDealer_ThenRowReturnedWithNullRepAndLocation()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle { Id = vehicleId, DealerId = dealerId, Registration = "V-001", ClaimedByRepId = null });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context, new RedirectOptions());

        // Act
        var result = await repository.GetFleetJobStateByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        var row = result[0];
        row.VehicleId.Should().Be(vehicleId);
        row.ClaimingRepId.Should().BeNull();
        row.RepState.Should().BeNull();
        row.HumanControlled.Should().BeFalse();
        row.ActiveRequestLatitude.Should().BeNull();
        row.ActiveRequestLongitude.Should().BeNull();
    }

    [Fact]
    public async Task GivenVehiclesInTwoDealers_WhenGetFleetJobStateByDealer_ThenOnlyOwnDealerVehiclesReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var otherDealerId = Guid.NewGuid();
        var ownVehicleId = Guid.NewGuid();

        context.Vehicles.AddRange(
            new Vehicle { Id = ownVehicleId, DealerId = dealerId, Registration = "V-001", ClaimedByRepId = null },
            new Vehicle { Id = Guid.NewGuid(), DealerId = otherDealerId, Registration = "V-002", ClaimedByRepId = null });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context, new RedirectOptions());

        // Act
        var result = await repository.GetFleetJobStateByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        result[0].VehicleId.Should().Be(ownVehicleId);
    }
}
