using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class ForceReleaseVehicleSignalRTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    // AC-4: Online rep receives VehicleForceReleased notification on RepHub when dispatcher calls force-release
    [Fact]
    public async Task GivenAnOnlineRepWithClaimedVehicle_WhenDispatcherCallsForceReleaseEndpoint_ThenRepReceivesVehicleForceReleasedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var httpClient = factory.CreateClient();

        // Rep1 claims Vehicle1
        var repToken = await GetTokenAsync(httpClient, "rep1@dealer.com", SeedConstants.DefaultPassword);
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);
        await httpClient.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Rep1 connects to RepHub
        var tcs = new TaskCompletionSource<VehicleForceReleasedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", repToken);
        connection.On<VehicleForceReleasedPayload>("VehicleForceReleased", tcs.SetResult);
        await connection.StartAsync();

        // Dispatcher logs in
        var dispatcherToken = await GetTokenAsync(httpClient, "alex@dealer.com", SeedConstants.DefaultPassword);
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", dispatcherToken);

        // Act
        await httpClient.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/force-release", null);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.VehicleId.Should().Be(SeedConstants.Vehicle1Id);
        received.Registration.Should().NotBeNullOrEmpty();

        await connection.StopAsync();
    }
}
