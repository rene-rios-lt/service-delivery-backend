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

// AC-3 (integration): an explicit release by a human-controlled rep clears HumanControlled in the
// database AND parks the vehicle (claim cleared). Drives the real endpoint → handler → persistence.
public class ReleaseClearsHumanControlledTests
{
    private static async Task SeedHumanControlledClaimAsync(CustomWebApplicationFactory factory, Guid vehicleId, Guid repId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vehicle = await db.Vehicles.FirstAsync(v => v.Id == vehicleId);
        vehicle.ClaimedByRepId = repId;
        vehicle.ClaimedAt = DateTime.UtcNow;

        db.RepSessions.Add(new RepSession
        {
            Id = Guid.NewGuid(),
            RepId = repId,
            VehicleId = vehicleId,
            StartedAt = DateTime.UtcNow
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            HumanControlled = true,
            LastHeartbeatAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
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
    public async Task GivenAHumanControlledRepWithAClaimedVehicle_WhenReleaseEndpointCalled_ThenHumanControlledFalseAndVehicleUnclaimed()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedHumanControlledClaimAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep1Id);
        var client = await AuthedClientAsync(factory, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.RepStateRecords.AsNoTracking().FirstAsync(r => r.RepId == SeedConstants.Rep1Id);
        state.HumanControlled.Should().BeFalse();
        state.State.Should().Be(RepState.Offline);

        var vehicle = await db.Vehicles.AsNoTracking().FirstAsync(v => v.Id == SeedConstants.Vehicle1Id);
        vehicle.ClaimedByRepId.Should().BeNull();
        vehicle.ClaimedAt.Should().BeNull();
    }
}
