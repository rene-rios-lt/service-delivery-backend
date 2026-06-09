using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class VehicleConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public VehicleConcurrencyTests()
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

    [Fact]
    public async Task GivenTwoSimultaneousClaims_WhenBothSavedConcurrently_ThenSecondSaveThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var rep1Id = Guid.NewGuid();
        var rep2Id = Guid.NewGuid();
        var initialRowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };

        using (var seedContext = new AppDbContext(_options))
        {
            var vehicle = new Vehicle
            {
                Id = vehicleId,
                DealerId = dealerId,
                Registration = "TEST-001",
                RowVersion = initialRowVersion
            };
            seedContext.Vehicles.Add(vehicle);
            await seedContext.SaveChangesAsync();
        }

        using var context1 = new AppDbContext(_options);
        using var context2 = new AppDbContext(_options);

        var repo1 = new VehicleRepository(context1);
        var repo2 = new VehicleRepository(context2);

        var vehicle1 = await repo1.GetByIdAsync(vehicleId);
        var vehicle2 = await repo2.GetByIdAsync(vehicleId);

        vehicle1!.ClaimedByRepId = rep1Id;
        vehicle1.ClaimedAt = DateTime.UtcNow;
        vehicle2!.ClaimedByRepId = rep2Id;
        vehicle2.ClaimedAt = DateTime.UtcNow;

        // Act — first save wins; SQLite trigger updates RowVersion to a new randomblob
        await repo1.UpdateAsync(vehicle1);

        // Assert — second context still holds the original RowVersion, so the WHERE clause
        // on the UPDATE finds 0 rows and EF raises DbUpdateConcurrencyException
        var act = () => repo2.UpdateAsync(vehicle2);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
