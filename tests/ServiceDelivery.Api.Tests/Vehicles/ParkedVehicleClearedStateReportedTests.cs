using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

// AC-4 (backend slice): after a heartbeat-timeout park, the fleet-state read (BE-027) reports the rep as
// Offline and not HumanControlled. The simulator-side enforcement of "reclaim the vehicle" is SIM-009 and
// out of scope here — this asserts only the backend reporting surface.
public class ParkedVehicleClearedStateReportedTests
{
    private record FleetStateVehicleResponse(
        Guid VehicleId,
        Guid? ClaimingRepId,
        string RepState,
        bool HumanControlled,
        object? ActiveRequestLocation);

    private static async Task<HttpClient> CreateSimulatorClientAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login",
            new LoginCommand("simulator@system.internal", SeedConstants.DefaultPassword));
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result!.Token);
        return client;
    }

    [Fact]
    public async Task GivenAParkedHumanControlledRep_WhenFleetStateRead_ThenRepReportedOfflineAndNotHumanControlled()
    {
        // Arrange — rep1 is human-controlled on a vehicle with a stale heartbeat.
        await using var factory = new CustomWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vehicle = db.Vehicles.Single(v => v.Id == SeedConstants.Vehicle1Id);
            vehicle.ClaimedByRepId = SeedConstants.Rep1Id;

            db.RepStateRecords.Add(new RepStateRecord
            {
                RepId = SeedConstants.Rep1Id,
                State = RepState.Available,
                HumanControlled = true,
                ActiveRequestId = null,
                LastHeartbeatAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        // Act — run the real timeout sweep, then read the fleet state as the simulator.
        using (var scope = factory.Services.CreateScope())
        {
            var sweeper = scope.ServiceProvider.GetRequiredService<IStaleHeartbeatSweeper>();
            await sweeper.SweepAsync(DateTimeOffset.UtcNow);
        }

        var client = await CreateSimulatorClientAsync(factory);
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FleetStateVehicleResponse[]>();
        var dto = body!.First(v => v.VehicleId == SeedConstants.Vehicle1Id);
        dto.RepState.Should().Be("Offline");
        dto.HumanControlled.Should().BeFalse();
    }
}
