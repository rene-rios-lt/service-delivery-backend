using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class UpdateVehiclePositionSignalRTests
{
    // AC-5: VehiclePositionUpdated received by a dispatcher via VehiclePositionHub
    [Fact]
    public async Task GivenASimulatorPostingPosition_WhenPositionUpdated_ThenDispatcherReceivesVehiclePositionUpdatedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        var dispatcherToken = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<VehiclePositionUpdatedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/position", dispatcherToken);
        connection.On<VehiclePositionUpdatedPayload>("VehiclePositionUpdated", tcs.SetResult);
        await connection.StartAsync();

        var simulatorToken = await HubTestHelpers.GetTokenAsync(factory, "simulator@system.internal", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", simulatorToken);

        var body = new { latitude = 51.5074, longitude = -0.1278, timestamp = DateTime.UtcNow };

        // Act
        await client.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position", body);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.VehicleId.Should().Be(SeedConstants.Vehicle1Id);
        received.Latitude.Should().Be(51.5074);
        received.Longitude.Should().Be(-0.1278);

        await connection.StopAsync();
    }

    // AC-6: RepPositionUpdated received by the assigned requester when request is Assigned
    [Fact]
    public async Task GivenAnEnRouteRepWithAssignedRequest_WhenPositionPosted_ThenRequesterReceivesRepPositionUpdatedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        // Set up rep in EnRoute state with an Assigned service request
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RepStateRecords.Add(new RepStateRecord
            {
                RepId = SeedConstants.Rep1Id,
                State = RepState.EnRoute,
                UpdatedAt = DateTime.UtcNow
            });
            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = Guid.NewGuid(),
                DealerId = SeedConstants.DealerId,
                RequesterId = SeedConstants.Bronze1Id,
                DtcId = SeedConstants.Dtc001Id,
                Latitude = 51.5074,
                Longitude = -0.1278,
                Status = ServiceRequestStatus.Assigned,
                AssignedRepId = SeedConstants.Rep1Id,
                CreatedAt = DateTime.UtcNow
            });
            // Claim vehicle for rep1
            var vehicle = db.Vehicles.First(v => v.Id == SeedConstants.Vehicle1Id);
            vehicle.ClaimedByRepId = SeedConstants.Rep1Id;
            await db.SaveChangesAsync();
        }

        var requesterToken = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepPositionUpdatedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requesterToken);
        connection.On<RepPositionUpdatedPayload>("RepPositionUpdated", tcs.SetResult);
        await connection.StartAsync();

        var simulatorToken = await HubTestHelpers.GetTokenAsync(factory, "simulator@system.internal", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", simulatorToken);

        var body = new { latitude = 51.5074, longitude = -0.1278, timestamp = DateTime.UtcNow };

        // Act
        await client.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position", body);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Latitude.Should().Be(51.5074);
        received.Longitude.Should().Be(-0.1278);
        received.EtaMinutes.Should().BeApproximately(0.0, precision: 0.01);

        await connection.StopAsync();
    }

    // AC-6: Does NOT broadcast RepPositionUpdated when rep has no active request
    [Fact]
    public async Task GivenARepWithNoAssignedRequest_WhenPositionPosted_ThenRequesterHubReceivesNoRepPositionUpdatedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        var requesterToken = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepPositionUpdatedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requesterToken);
        connection.On<RepPositionUpdatedPayload>("RepPositionUpdated", tcs.SetResult);
        await connection.StartAsync();

        var simulatorToken = await HubTestHelpers.GetTokenAsync(factory, "simulator@system.internal", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", simulatorToken);

        var body = new { latitude = 51.5074, longitude = -0.1278, timestamp = DateTime.UtcNow };

        // Act — Vehicle1 has no claimed rep, no active request
        await client.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position", body);

        // Assert — no event received within 2 seconds
        var completed = tcs.Task.IsCompleted;
        await Task.Delay(2000);
        tcs.Task.IsCompleted.Should().BeFalse("RequesterHub should NOT fire RepPositionUpdated when rep has no active request");

        await connection.StopAsync();
    }
}
