using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Rep;

// AC-2 (end-to-end): drives the real IStaleHeartbeatSweeper against the real in-memory DB. A stale
// human-controlled rep with an active job must end Offline with HumanControlled cleared, and its request
// re-queued to Pending via the delegated RepWentOfflineCommand path.
public class HeartbeatTimeoutSweepTests
{
    [Fact]
    public async Task GivenAStaleHumanControlledRepWithActiveJob_WhenSwept_ThenJobIsReturnedToPendingAndReMatched()
    {
        // Arrange — rep1 is human-controlled with a stale heartbeat and an Assigned request.
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        var staleHeartbeat = DateTime.UtcNow.AddMinutes(-5);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = requestId,
                DealerId = SeedConstants.DealerId,
                RequesterId = SeedConstants.Bronze1Id,
                DtcId = SeedConstants.Dtc001Id,
                Latitude = 41.6,
                Longitude = -93.6,
                Status = ServiceRequestStatus.Assigned,
                Tier = ServiceTier.Bronze,
                AssignedRepId = SeedConstants.Rep1Id,
                CreatedAt = DateTime.UtcNow
            });

            db.RepStateRecords.Add(new RepStateRecord
            {
                RepId = SeedConstants.Rep1Id,
                State = RepState.EnRoute,
                ActiveRequestId = requestId,
                HumanControlled = true,
                LastHeartbeatAt = staleHeartbeat,
                UpdatedAt = staleHeartbeat
            });

            await db.SaveChangesAsync();
        }

        // Act — run the real sweeper (asOf now; default 45s timeout makes the 5-minute-old heartbeat stale).
        using (var scope = factory.Services.CreateScope())
        {
            var sweeper = scope.ServiceProvider.GetRequiredService<IStaleHeartbeatSweeper>();
            await sweeper.SweepAsync(DateTimeOffset.UtcNow);
        }

        // Assert
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await verifyDb.RepStateRecords.AsNoTracking().FirstAsync(r => r.RepId == SeedConstants.Rep1Id);
        state.State.Should().Be(RepState.Offline);
        state.HumanControlled.Should().BeFalse();

        var request = await verifyDb.ServiceRequests.AsNoTracking().FirstAsync(r => r.Id == requestId);
        request.Status.Should().Be(ServiceRequestStatus.Pending);
        request.AssignedRepId.Should().BeNull();
    }

    [Fact]
    public async Task GivenAHumanControlledRepWithRecentHeartbeat_WhenSwept_ThenRepIsLeftUntouched()
    {
        // Arrange — fresh heartbeat (now) must be excluded from the sweep.
        await using var factory = new CustomWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RepStateRecords.Add(new RepStateRecord
            {
                RepId = SeedConstants.Rep1Id,
                State = RepState.Available,
                HumanControlled = true,
                LastHeartbeatAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Act
        using (var scope = factory.Services.CreateScope())
        {
            var sweeper = scope.ServiceProvider.GetRequiredService<IStaleHeartbeatSweeper>();
            await sweeper.SweepAsync(DateTimeOffset.UtcNow);
        }

        // Assert
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await verifyDb.RepStateRecords.AsNoTracking().FirstAsync(r => r.RepId == SeedConstants.Rep1Id);
        state.State.Should().Be(RepState.Available);
        state.HumanControlled.Should().BeTrue();
    }
}
