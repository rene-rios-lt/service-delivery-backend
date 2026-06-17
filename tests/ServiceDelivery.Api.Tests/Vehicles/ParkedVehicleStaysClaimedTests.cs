using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Rep.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

// AC-3 (backend half): when a rep with an active job goes Offline mid-job, the vehicle "parks" —
// it stays Claimed by the same rep, with its claim untouched. This is an integration test because
// the assertion is "the real offline path does NOT release the vehicle against real persistence":
// the handler has no IVehicleRepository collaborator, so a unit test could only assert on the test's
// own mock. Here the real RepWentOfflineCommandHandler runs via IMediator against the in-memory DB,
// and the vehicle is re-read from the database after handling to prove the claim row is unchanged.
public class ParkedVehicleStaysClaimedTests
{
    [Fact]
    public async Task GivenARepWithAClaimedVehicleAndActiveJob_WhenRepGoesOfflineMidJob_ThenVehicleStaysClaimedBySameRep()
    {
        // Arrange — real DB: rep1 has an Assigned ServiceRequest and a Vehicle Claimed by rep1.
        await using var factory = new CustomWebApplicationFactory();

        var requestId = Guid.NewGuid();
        var claimedAt = new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var vehicle = await db.Vehicles.FirstAsync(v => v.Id == SeedConstants.Vehicle1Id);
            vehicle.ClaimedByRepId = SeedConstants.Rep1Id;
            vehicle.ClaimedAt = claimedAt;

            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = requestId,
                DealerId = SeedConstants.DealerId,
                RequesterId = SeedConstants.Bronze1Id,
                DtcId = SeedConstants.Dtc001Id,
                Latitude = 0,
                Longitude = 0,
                Status = ServiceRequestStatus.Assigned,
                Tier = ServiceTier.Bronze,
                AssignedRepId = SeedConstants.Rep1Id,
                CreatedAt = DateTime.UtcNow
            });

            var repState = await db.RepStateRecords.FirstOrDefaultAsync(r => r.RepId == SeedConstants.Rep1Id);
            if (repState is null)
            {
                db.RepStateRecords.Add(new RepStateRecord
                {
                    RepId = SeedConstants.Rep1Id,
                    State = RepState.EnRoute,
                    ActiveRequestId = requestId,
                    HumanControlled = false,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                repState.State = RepState.EnRoute;
                repState.ActiveRequestId = requestId;
                repState.HumanControlled = false;
                repState.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }

        // Act — drive the real offline path end-to-end through the app's mediator pipeline,
        // so the real RepWentOfflineCommandHandler executes against real persistence.
        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new RepWentOfflineCommand(SeedConstants.Rep1Id, SeedConstants.DealerId));
        }

        // Assert — re-read the vehicle from the database: it must still be Claimed by the same rep,
        // claim timestamp unchanged. This fails if the handler were to release/re-status the vehicle.
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var parkedVehicle = await verifyDb.Vehicles.AsNoTracking().FirstAsync(v => v.Id == SeedConstants.Vehicle1Id);
        parkedVehicle.ClaimedByRepId.Should().Be(SeedConstants.Rep1Id);
        parkedVehicle.ClaimedAt.Should().Be(claimedAt);

        // Sanity: the offline handler genuinely ran (request re-queued to Pending), so the vehicle
        // assertion above is exercising real handler behaviour, not an inert arrangement.
        var requeued = await verifyDb.ServiceRequests.AsNoTracking().FirstAsync(r => r.Id == requestId);
        requeued.Status.Should().Be(ServiceRequestStatus.Pending);
        requeued.AssignedRepId.Should().BeNull();
    }
}
