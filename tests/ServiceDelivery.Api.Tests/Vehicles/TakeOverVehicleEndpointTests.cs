using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class TakeOverVehicleEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private async Task SetRepTokenAsync(HttpClient client, string repEmail)
    {
        var token = await GetTokenAsync(client, repEmail, SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task SeedIdleSimulatorVehicleAsync(
        CustomWebApplicationFactory factory,
        Guid vehicleId,
        Guid displacedRepId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vehicle = await db.Vehicles.FirstAsync(v => v.Id == vehicleId);
        vehicle.ClaimedByRepId = displacedRepId;
        vehicle.ClaimedAt = DateTime.UtcNow;
        vehicle.LastLatitude = 41.6;
        vehicle.LastLongitude = -93.6;

        db.RepSessions.Add(new RepSession
        {
            Id = Guid.NewGuid(),
            RepId = displacedRepId,
            VehicleId = vehicleId,
            StartedAt = DateTime.UtcNow
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = displacedRepId,
            State = RepState.Available,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedBusyVehicleAsync(
        CustomWebApplicationFactory factory,
        Guid vehicleId,
        Guid displacedRepId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vehicle = await db.Vehicles.FirstAsync(v => v.Id == vehicleId);
        vehicle.ClaimedByRepId = displacedRepId;
        vehicle.ClaimedAt = DateTime.UtcNow;

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = displacedRepId,
            State = RepState.EnRoute,
            ActiveRequestId = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedEnRouteCallerAsync(
        CustomWebApplicationFactory factory,
        Guid callerRepId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = callerRepId,
            State = RepState.EnRoute,
            ActiveRequestId = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    // AC-4: 200 with the new session on the happy path
    [Fact]
    public async Task GivenAnIdleRepAndIdleVehicle_WhenTakeOverEndpointCalled_ThenReturns200WithSession()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedIdleSimulatorVehicleAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep2Id);
        var client = factory.CreateClient();
        await SetRepTokenAsync(client, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/take-over", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TakeOverVehicleResult>();
        result.Should().NotBeNull();
        result!.VehicleId.Should().Be(SeedConstants.Vehicle1Id);
        result.RepId.Should().Be(SeedConstants.Rep1Id);
        result.RepState.Should().Be("Available");
        result.SessionId.Should().NotBeEmpty();
    }

    // AC-2 (integration): vehicle is claimed by the caller in the DB
    [Fact]
    public async Task GivenAnIdleVehicle_WhenTakeOverEndpointCalled_ThenVehicleClaimedByCallerInDb()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedIdleSimulatorVehicleAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep2Id);
        var client = factory.CreateClient();
        await SetRepTokenAsync(client, "rep1@dealer.com");

        // Act
        await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/take-over", null);

        // Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vehicle = await db.Vehicles.FirstAsync(v => v.Id == SeedConstants.Vehicle1Id);
        vehicle.ClaimedByRepId.Should().Be(SeedConstants.Rep1Id);
        vehicle.ClaimedAt.Should().NotBeNull();
    }

    // AC-2 (integration): caller RepStateRecord is HumanControlled with heartbeat stamped
    [Fact]
    public async Task GivenAnIdleRep_WhenTakeOverEndpointCalled_ThenRepStateRecordHasHumanControlledAndHeartbeat()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedIdleSimulatorVehicleAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep2Id);
        var client = factory.CreateClient();
        await SetRepTokenAsync(client, "rep1@dealer.com");

        // Act
        await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/take-over", null);

        // Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.RepStateRecords.FirstAsync(r => r.RepId == SeedConstants.Rep1Id);
        state.State.Should().Be(RepState.Available);
        state.HumanControlled.Should().BeTrue();
        state.LastHeartbeatAt.Should().NotBeNull();
    }

    // AC-1 / AC-4 (integration): 409 with reason when the caller rep is not idle
    [Fact]
    public async Task GivenAnEnRouteRep_WhenTakeOverEndpointCalled_ThenReturns409WithReason()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedIdleSimulatorVehicleAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep2Id);
        await SeedEnRouteCallerAsync(factory, SeedConstants.Rep1Id);
        var client = factory.CreateClient();
        await SetRepTokenAsync(client, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/take-over", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ConflictBody>();
        body.Should().NotBeNull();
        body!.Reason.Should().NotBeNullOrWhiteSpace();
    }

    // AC-1 / AC-4 (integration): 409 with reason when the target vehicle's rep is busy
    [Fact]
    public async Task GivenAVehicleWithABusyRep_WhenTakeOverEndpointCalled_ThenReturns409WithReason()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedBusyVehicleAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep2Id);
        var client = factory.CreateClient();
        await SetRepTokenAsync(client, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/take-over", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ConflictBody>();
        body.Should().NotBeNull();
        body!.Reason.Should().NotBeNullOrWhiteSpace();
    }

    // AC-1 (integration): 404 when the vehicle does not exist
    [Fact]
    public async Task GivenANonExistentVehicle_WhenTakeOverEndpointCalled_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetRepTokenAsync(client, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync($"/vehicles/{Guid.NewGuid()}/take-over", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-5: 403 when a dispatcher token is used
    [Fact]
    public async Task GivenADispatcherToken_WhenTakeOverEndpointCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/take-over", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // AC-5: 401 when no authorization header is present
    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenTakeOverEndpointCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/take-over", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record ConflictBody(string Reason);
}
