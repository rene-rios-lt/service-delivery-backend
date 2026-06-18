using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

// AC-5: a vehicle parked by a heartbeat timeout (its rep taken Offline, HumanControlled cleared) remains
// available to the fleet — it accepts a fresh take-over by an idle rep and a dispatcher force-release.
public class ParkedVehicleRemainsAvailableTests
{
    private static async Task ParkVehicleByTimeoutAsync(CustomWebApplicationFactory factory, Guid vehicleId, Guid repId)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vehicle = await db.Vehicles.FirstAsync(v => v.Id == vehicleId);
            vehicle.ClaimedByRepId = repId;
            vehicle.ClaimedAt = DateTime.UtcNow.AddMinutes(-5);
            vehicle.LastLatitude = 41.6;
            vehicle.LastLongitude = -93.6;

            db.RepStateRecords.Add(new RepStateRecord
            {
                RepId = repId,
                State = RepState.Available,
                HumanControlled = true,
                ActiveRequestId = null,
                LastHeartbeatAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var sweeper = scope.ServiceProvider.GetRequiredService<IStaleHeartbeatSweeper>();
            await sweeper.SweepAsync(DateTimeOffset.UtcNow);
        }
    }

    private static async Task<HttpClient> AuthedClientAsync(CustomWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, SeedConstants.DefaultPassword));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        return client;
    }

    [Fact]
    public async Task GivenAParkedVehicleAfterTimeout_WhenTakeOverPosted_ThenTakeOverSucceeds()
    {
        // Arrange — rep1's vehicle was parked by a timeout; rep2 (idle) attempts a fresh take-over.
        await using var factory = new CustomWebApplicationFactory();
        await ParkVehicleByTimeoutAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep1Id);
        var client = await AuthedClientAsync(factory, "rep2@dealer.com");

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/take-over", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TakeOverVehicleResult>();
        result!.RepId.Should().Be(SeedConstants.Rep2Id);
        result.VehicleId.Should().Be(SeedConstants.Vehicle1Id);
    }

    [Fact]
    public async Task GivenAParkedVehicleAfterTimeout_WhenForceReleasePosted_ThenForceReleaseSucceeds()
    {
        // Arrange — rep1's vehicle was parked by a timeout; a dispatcher force-releases it.
        await using var factory = new CustomWebApplicationFactory();
        await ParkVehicleByTimeoutAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep1Id);
        var client = await AuthedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/force-release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vehicle = await db.Vehicles.AsNoTracking().FirstAsync(v => v.Id == SeedConstants.Vehicle1Id);
        vehicle.ClaimedByRepId.Should().BeNull();
    }
}
