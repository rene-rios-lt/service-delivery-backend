using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class VehicleRepositoryDispatcherFleetTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VehicleRepositoryDispatcherFleetTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenAClaimedRepWithActiveRequest_WhenGetDispatcherFleetByDealer_ThenAllFieldsPopulatedFromJoin()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-001",
            ClaimedByRepId = repId,
            LastLatitude = 41.5,
            LastLongitude = -93.6
        });
        context.Users.Add(new User
        {
            Id = repId,
            Name = "Riley Rep",
            Email = "riley@dealer.com",
            Role = UserRole.ServiceRep,
            DealerId = dealerId
        });
        context.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.EnRoute,
            HumanControlled = true,
            ActiveRequestId = requestId
        });
        context.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = dealerId,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId
        });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetDispatcherFleetByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        var row = result[0];
        row.VehicleId.Should().Be(vehicleId);
        row.Registration.Should().Be("V-001");
        row.ClaimingRepId.Should().Be(repId);
        row.RepName.Should().Be("Riley Rep");
        row.RepState.Should().Be(RepState.EnRoute);
        row.HumanControlled.Should().BeTrue();
        row.LastLatitude.Should().Be(41.5);
        row.LastLongitude.Should().Be(-93.6);
        row.ActiveRequestId.Should().Be(requestId);
        row.ActiveRequestTier.Should().Be(ServiceTier.Gold);
    }

    [Fact]
    public async Task GivenAClaimedRepWithActiveRequestAndDtc_WhenGetDispatcherFleetByDealer_ThenActiveRequestTitleEqualsHumanReadableDtcTitle()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-001",
            ClaimedByRepId = repId,
            LastLatitude = 41.5,
            LastLongitude = -93.6
        });
        context.Users.Add(new User
        {
            Id = repId,
            Name = "Riley Rep",
            Email = "riley@dealer.com",
            Role = UserRole.ServiceRep,
            DealerId = dealerId
        });
        context.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.EnRoute,
            HumanControlled = true,
            ActiveRequestId = requestId
        });
        context.DiagnosticTroubleCodes.Add(new DiagnosticTroubleCode
        {
            Id = dtcId,
            DealerId = dealerId,
            Code = "DTC-001",
            HumanReadableTitle = "Hydraulic system fault",
            RequiredEquipmentType = EquipmentType.HydraulicTool
        });
        context.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = dealerId,
            DtcId = dtcId,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId
        });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetDispatcherFleetByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        var row = result[0];
        row.ActiveRequestId.Should().Be(requestId);
        row.ActiveRequestTitle.Should().Be("Hydraulic system fault");
    }

    [Fact]
    public async Task GivenAClaimedRepWithNoActiveRequest_WhenGetDispatcherFleetByDealer_ThenActiveRequestTitleIsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-001",
            ClaimedByRepId = repId,
            LastLatitude = 10.0,
            LastLongitude = 20.0
        });
        context.Users.Add(new User
        {
            Id = repId,
            Name = "Riley Rep",
            Email = "riley@dealer.com",
            Role = UserRole.ServiceRep,
            DealerId = dealerId
        });
        context.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            HumanControlled = false,
            ActiveRequestId = null
        });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetDispatcherFleetByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        result[0].ActiveRequestTitle.Should().BeNull();
    }

    [Fact]
    public async Task GivenAClaimedIdleRep_WhenGetDispatcherFleetByDealer_ThenActiveRequestFieldsAreNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-001",
            ClaimedByRepId = repId,
            LastLatitude = 10.0,
            LastLongitude = 20.0
        });
        context.Users.Add(new User
        {
            Id = repId,
            Name = "Riley Rep",
            Email = "riley@dealer.com",
            Role = UserRole.ServiceRep,
            DealerId = dealerId
        });
        context.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            HumanControlled = false,
            ActiveRequestId = null
        });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetDispatcherFleetByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        var row = result[0];
        row.ClaimingRepId.Should().Be(repId);
        row.RepName.Should().Be("Riley Rep");
        row.RepState.Should().Be(RepState.Available);
        row.ActiveRequestId.Should().BeNull();
        row.ActiveRequestTier.Should().BeNull();
    }

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenGetDispatcherFleetByDealer_ThenRepFieldsNullAndVehicleFieldsPopulated()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-002",
            ClaimedByRepId = null,
            LastLatitude = 5.0,
            LastLongitude = 6.0
        });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetDispatcherFleetByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        var row = result[0];
        row.VehicleId.Should().Be(vehicleId);
        row.Registration.Should().Be("V-002");
        row.ClaimingRepId.Should().BeNull();
        row.RepName.Should().BeNull();
        row.RepState.Should().BeNull();
        row.HumanControlled.Should().BeFalse();
        row.LastLatitude.Should().Be(5.0);
        row.LastLongitude.Should().Be(6.0);
        row.ActiveRequestId.Should().BeNull();
        row.ActiveRequestTier.Should().BeNull();
    }

    [Fact]
    public async Task GivenAVehicleWithNoPosition_WhenGetDispatcherFleetByDealer_ThenLatitudeAndLongitudeAreNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        context.Vehicles.Add(new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-003",
            ClaimedByRepId = null,
            LastLatitude = null,
            LastLongitude = null
        });
        await context.SaveChangesAsync();

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetDispatcherFleetByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        result[0].LastLatitude.Should().BeNull();
        result[0].LastLongitude.Should().BeNull();
    }

    [Fact]
    public async Task GivenVehiclesInTwoDealers_WhenGetDispatcherFleetByDealer_ThenOnlyOwnDealerRowsReturned()
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

        var repository = new VehicleRepository(context);

        // Act
        var result = await repository.GetDispatcherFleetByDealerAsync(dealerId);

        // Assert
        result.Should().HaveCount(1);
        result[0].VehicleId.Should().Be(ownVehicleId);
    }
}
