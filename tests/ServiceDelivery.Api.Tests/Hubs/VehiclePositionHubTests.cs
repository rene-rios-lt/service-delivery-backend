using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Hubs;

[Collection("Hub Tests")]
public class VehiclePositionHubTests
{
    [Fact]
    public async Task GivenADispatcherConnection_WhenVehiclePositionUpdatedIsSent_ThenDispatcherReceivesVehiclePositionUpdatedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<VehiclePositionUpdatedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/position", token);
        connection.On<VehiclePositionUpdatedPayload>("VehiclePositionUpdated", tcs.SetResult);
        await connection.StartAsync();

        var repId = SeedConstants.Rep1Id;
        var vehicleId = SeedConstants.Vehicle1Id;
        var payload = new VehiclePositionUpdatedPayload(repId, vehicleId, 40.7128, -74.0060, "Available");
        var dealerGroup = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IVehiclePositionHubService>();
        await hubService.SendVehiclePositionUpdatedAsync(dealerGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        received.RepId.Should().Be(repId);
        received.VehicleId.Should().Be(vehicleId);
        received.Latitude.Should().Be(40.7128);
        received.Longitude.Should().Be(-74.0060);
        received.State.Should().Be("Available");

        await connection.StopAsync();
    }
}
