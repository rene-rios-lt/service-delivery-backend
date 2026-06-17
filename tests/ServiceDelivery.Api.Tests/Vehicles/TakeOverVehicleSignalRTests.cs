using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

[Collection("Hub Tests")]
public class TakeOverVehicleSignalRTests
{
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

    private static async Task TakeOverAsRepAsync(CustomWebApplicationFactory factory, Guid vehicleId)
    {
        var repToken = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);
        await client.PostAsync($"/vehicles/{vehicleId}/take-over", null);
    }

    // AC-3: a connected dispatcher receives RepStateChanged (caller → Available) over DispatchHub
    [Fact]
    public async Task GivenAConnectedDispatcher_WhenRepTakesOverVehicle_ThenReceivesRepStateChangedWithAvailable()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedIdleSimulatorVehicleAsync(factory, SeedConstants.Vehicle1Id, SeedConstants.Rep2Id);

        var dispatcherToken = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepStateChangedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dispatcherToken);
        connection.On<RepStateChangedPayload>("RepStateChanged", payload =>
        {
            if (payload.RepId == SeedConstants.Rep1Id)
                tcs.TrySetResult(payload);
        });
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await TakeOverAsRepAsync(factory, SeedConstants.Vehicle1Id);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(SeedConstants.Rep1Id);
        received.NewState.Should().Be("Available");

        await connection.StopAsync();
    }
}
