using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

// AC-2: the release-then-claim must be atomic under optimistic concurrency. This test exercises the
// real TakeOverVehicleCommandHandler against SQLite (with the same RowVersion trigger the existing
// VehicleConcurrencyTests use), because the InMemory provider used by Api.Tests cannot bump the
// RowVersion token and therefore cannot surface a genuine DbUpdateConcurrencyException.
public class TakeOverVehicleConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TakeOverVehicleConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TRIGGER IF NOT EXISTS vehicles_rowversion_update
            AFTER UPDATE ON Vehicles
            BEGIN
                UPDATE Vehicles SET RowVersion = randomblob(8) WHERE rowid = NEW.rowid;
            END;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private TakeOverVehicleCommandHandler BuildHandler(AppDbContext context)
    {
        return new TakeOverVehicleCommandHandler(
            new VehicleRepository(context),
            new RepSessionRepository(context),
            new RepStateRepository(context),
            new NoOpDispatchHubService());
    }

    // Minimal stub: this test isolates the persistence/concurrency behaviour, so the broadcast is a
    // no-op. Broadcast behaviour is verified separately in the Application and Api SignalR tests.
    private sealed class NoOpDispatchHubService : IDispatchHubService
    {
        public Task SendServiceRequestPendingAsync(string dealerGroup, ServiceRequestPendingPayload payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendServiceRequestAssignedAsync(string dealerGroup, ServiceRequestAssignedPayload payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendServiceRequestCompletedAsync(string dealerGroup, ServiceRequestCompletedPayload payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendRepStateChangedAsync(string dealerGroup, RepStateChangedPayload payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendRepOfflineMidJobAsync(string dealerGroup, RepOfflineMidJobPayload payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendFleetPositionUpdateAsync(string dealerGroup, FleetPositionUpdatePayload payload, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GivenTwoSimultaneousTakeOversOfTheSameVehicle_WhenBothPersist_ThenOneSucceedsAndTheOtherGets409()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var callerA = Guid.NewGuid();
        var callerB = Guid.NewGuid();

        using (var seed = new AppDbContext(_options))
        {
            seed.Vehicles.Add(new Vehicle
            {
                Id = vehicleId,
                DealerId = dealerId,
                Registration = "TEST-001",
                ClaimedByRepId = displacedRepId,
                ClaimedAt = DateTime.UtcNow,
                LastLatitude = 41.6,
                LastLongitude = -93.6,
                RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }
            });
            seed.RepSessions.Add(new RepSession
            {
                Id = Guid.NewGuid(),
                RepId = displacedRepId,
                VehicleId = vehicleId,
                StartedAt = DateTime.UtcNow
            });
            seed.RepStateRecords.Add(new RepStateRecord
            {
                RepId = displacedRepId,
                State = RepState.Available,
                UpdatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        using var contextA = new AppDbContext(_options);
        using var contextB = new AppDbContext(_options);
        var handlerA = BuildHandler(contextA);
        var handlerB = BuildHandler(contextB);

        // Both handlers load the vehicle at the same RowVersion before either persists.
        var vehicleA = await contextA.Vehicles.FirstAsync(v => v.Id == vehicleId);
        var vehicleB = await contextB.Vehicles.FirstAsync(v => v.Id == vehicleId);
        vehicleA.Should().NotBeNull();
        vehicleB.Should().NotBeNull();

        // Act — A wins; the SQLite trigger bumps RowVersion so B's stale token loses.
        await handlerA.Handle(new TakeOverVehicleCommand(vehicleId, callerA), CancellationToken.None);

        var actB = () => handlerB.Handle(new TakeOverVehicleCommand(vehicleId, callerB), CancellationToken.None);

        // Assert — the loser raises the concurrency exception the endpoint maps to 409.
        await actB.Should().ThrowAsync<DbUpdateConcurrencyException>();

        using var verify = new AppDbContext(_options);
        var persisted = await verify.Vehicles.FirstAsync(v => v.Id == vehicleId);
        persisted.ClaimedByRepId.Should().Be(callerA);
    }
}
