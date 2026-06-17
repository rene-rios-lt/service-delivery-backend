using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class ForceReleaseParkedVehicleTests
{
    private static async Task<HttpClient> AuthedClientAsync(CustomWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, SeedConstants.DefaultPassword));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        return client;
    }

    // AC-6: a vehicle claimed by a rep who then went Offline mid-job (the vehicle "parks"
    // and stays Claimed) is still force-releasable by a dispatcher.
    [Fact]
    public async Task GivenAVehicleClaimedByARepWhoWentOfflineMidJob_WhenDispatcherForceReleases_ThenVehicleIsReleased()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        var repClient = await AuthedClientAsync(factory, "rep1@dealer.com");
        await repClient.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Simulate the disconnect outcome: the rep has gone Offline but the vehicle stays Claimed (parked).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await db.RepStateRecords.FirstOrDefaultAsync(r => r.RepId == SeedConstants.Rep1Id);
            if (existing is null)
            {
                db.RepStateRecords.Add(new RepStateRecord
                {
                    RepId = SeedConstants.Rep1Id,
                    State = RepState.Offline,
                    ActiveRequestId = null,
                    HumanControlled = false,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.State = RepState.Offline;
                existing.ActiveRequestId = null;
                existing.HumanControlled = false;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }

        var dispatcherClient = await AuthedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await dispatcherClient.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/force-release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vehicle = await verifyDb.Vehicles.AsNoTracking().FirstAsync(v => v.Id == SeedConstants.Vehicle1Id);
        vehicle.ClaimedByRepId.Should().BeNull();
    }
}
