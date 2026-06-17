using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class RepStateRepositoryTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid OtherDealerId = Guid.NewGuid();

    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RepStateRepositoryTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static Vehicle Vehicle(Guid id, Guid dealerId, double lat, double lng, params EquipmentType[] equipment)
        => new()
        {
            Id = id,
            DealerId = dealerId,
            Registration = $"V-{id.ToString()[..4]}",
            LastLatitude = lat,
            LastLongitude = lng,
            LastPositionUpdatedAt = DateTime.UtcNow,
            Equipment = equipment.Select(e => new VehicleEquipment { VehicleId = id, EquipmentType = e }).ToList()
        };

    private static RepSession ActiveSession(Guid repId, Guid vehicleId)
        => new() { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };

    private static RepStateRecord State(Guid repId, RepState state, DateTime updatedAt)
        => new() { RepId = repId, State = state, UpdatedAt = updatedAt };

    [Fact]
    public async Task GivenAnAvailableRepWithClaimedEquippedVehicle_WhenGetAvailableByDealer_ThenCandidateReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var updatedAt = new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc);
        context.Vehicles.Add(Vehicle(vehicleId, DealerId, 12.5, -34.25, EquipmentType.HydraulicTool, EquipmentType.BrakingSystemKit));
        context.RepSessions.Add(ActiveSession(repId, vehicleId));
        context.RepStateRecords.Add(State(repId, RepState.Available, updatedAt));
        await context.SaveChangesAsync();
        var repository = new RepStateRepository(context);

        // Act
        var candidates = await repository.GetAvailableByDealerAsync(DealerId);

        // Assert
        candidates.Should().HaveCount(1);
        candidates[0].RepId.Should().Be(repId);
        candidates[0].VehicleLatitude.Should().Be(12.5);
        candidates[0].VehicleLongitude.Should().Be(-34.25);
        candidates[0].AvailableSince.Should().Be(updatedAt);
        candidates[0].Equipment.Should().BeEquivalentTo(new[] { EquipmentType.HydraulicTool, EquipmentType.BrakingSystemKit });
    }

    [Fact]
    public async Task GivenNonAvailableReps_WhenGetAvailableByDealer_ThenTheyAreExcluded()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var availableRep = Guid.NewGuid();
        var enRouteRep = Guid.NewGuid();
        var offlineRep = Guid.NewGuid();
        var availableVehicle = Guid.NewGuid();
        var enRouteVehicle = Guid.NewGuid();
        var offlineVehicle = Guid.NewGuid();

        context.Vehicles.AddRange(
            Vehicle(availableVehicle, DealerId, 1.0, 1.0, EquipmentType.HydraulicTool),
            Vehicle(enRouteVehicle, DealerId, 2.0, 2.0, EquipmentType.HydraulicTool),
            Vehicle(offlineVehicle, DealerId, 3.0, 3.0, EquipmentType.HydraulicTool));
        context.RepSessions.AddRange(
            ActiveSession(availableRep, availableVehicle),
            ActiveSession(enRouteRep, enRouteVehicle),
            ActiveSession(offlineRep, offlineVehicle));
        context.RepStateRecords.AddRange(
            State(availableRep, RepState.Available, DateTime.UtcNow),
            State(enRouteRep, RepState.EnRoute, DateTime.UtcNow),
            State(offlineRep, RepState.Offline, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var repository = new RepStateRepository(context);

        // Act
        var candidates = await repository.GetAvailableByDealerAsync(DealerId);

        // Assert
        candidates.Should().ContainSingle(c => c.RepId == availableRep);
        candidates.Should().NotContain(c => c.RepId == enRouteRep);
        candidates.Should().NotContain(c => c.RepId == offlineRep);
    }

    [Fact]
    public async Task GivenAnAvailableRepInAnotherDealer_WhenGetAvailableByDealer_ThenExcluded()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        context.Vehicles.Add(Vehicle(vehicleId, OtherDealerId, 1.0, 1.0, EquipmentType.HydraulicTool));
        context.RepSessions.Add(ActiveSession(repId, vehicleId));
        context.RepStateRecords.Add(State(repId, RepState.Available, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var repository = new RepStateRepository(context);

        // Act
        var candidates = await repository.GetAvailableByDealerAsync(DealerId);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenAnExistingHumanControlledRecord_WhenADetachedClearedRecordIsUpserted_ThenHumanControlledIsPersistedAsFalse()
    {
        // Arrange — an existing tracked record with the marker set true.
        var databaseName = $"RepStateRepositoryTests_{Guid.NewGuid()}";
        var repId = Guid.NewGuid();
        using (var seedContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                   .UseInMemoryDatabase(databaseName).Options))
        {
            seedContext.RepStateRecords.Add(new RepStateRecord
            {
                RepId = repId,
                State = RepState.OnSite,
                ActiveRequestId = Guid.NewGuid(),
                HumanControlled = true,
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await seedContext.SaveChangesAsync();
        }

        // A separate context receives a detached record whose marker has been cleared.
        using var upsertContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName).Options);
        var repository = new RepStateRepository(upsertContext);
        var cleared = new RepStateRecord
        {
            RepId = repId,
            State = RepState.Offline,
            ActiveRequestId = null,
            HumanControlled = false,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await repository.UpsertAsync(cleared);

        // Assert
        using var verifyContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName).Options);
        var reloaded = await new RepStateRepository(verifyContext).GetByRepIdAsync(repId);
        reloaded!.HumanControlled.Should().BeFalse();
        reloaded.State.Should().Be(RepState.Offline);
    }

    [Fact]
    public async Task GivenAnAvailableRepWithEndedSession_WhenGetAvailableByDealer_ThenExcluded()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        context.Vehicles.Add(Vehicle(vehicleId, DealerId, 1.0, 1.0, EquipmentType.HydraulicTool));
        context.RepSessions.Add(new RepSession
        {
            Id = Guid.NewGuid(),
            RepId = repId,
            VehicleId = vehicleId,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            EndedAt = DateTime.UtcNow.AddHours(-1)
        });
        context.RepStateRecords.Add(State(repId, RepState.Available, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var repository = new RepStateRepository(context);

        // Act
        var candidates = await repository.GetAvailableByDealerAsync(DealerId);

        // Assert
        candidates.Should().BeEmpty();
    }
}
