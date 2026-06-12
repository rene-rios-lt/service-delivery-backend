using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Matching;

public class MatchingIntegrationTests
{
    private static async Task<string> GetTokenAsync(HttpClient client, string email)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, SeedConstants.DefaultPassword));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private static void SetBearer(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task GivenASubmittedRequestWithACandidate_WhenMatched_ThenRepReceivesJobOfferReceivedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var httpClient = factory.CreateClient();

        // Rep1 claims an equipped vehicle (Vehicle1 carries HydraulicTool → DTC-001)
        var repToken = await GetTokenAsync(httpClient, "rep1@dealer.com");
        SetBearer(httpClient, repToken);
        await httpClient.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Simulator gives the claimed vehicle a known position
        var simulatorToken = await GetTokenAsync(httpClient, "simulator@system.internal");
        SetBearer(httpClient, simulatorToken);
        await httpClient.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position",
            new { latitude = 37.7749, longitude = -122.4194, timestamp = DateTime.UtcNow });

        // Rep1 connects to RepHub and listens for the offer
        var tcs = new TaskCompletionSource<JobOfferReceivedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", repToken);
        connection.On<JobOfferReceivedPayload>("JobOfferReceived", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // A requester submits a DTC-001 request at the vehicle's location
        var requesterToken = await GetTokenAsync(httpClient, "gold1@example.com");
        SetBearer(httpClient, requesterToken);

        // Act
        await httpClient.PostAsJsonAsync("/service-requests",
            new { dtcId = SeedConstants.Dtc001Id, latitude = 37.7749, longitude = -122.4194 });

        // Assert
        var payload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        payload.RequestId.Should().NotBeEmpty();
        payload.RequesterName.Should().Be("Gold User 1");
        payload.DtcTitle.Should().Be("Hydraulic system fault");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenASubmittedRequestWithNoCandidate_WhenMatched_ThenDispatchersReceiveServiceRequestPendingEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var httpClient = factory.CreateClient();

        // No rep has claimed a vehicle, so there are no Available candidates.
        // Dispatcher connects to DispatchHub and listens for the pending broadcast.
        var dispatcherToken = await GetTokenAsync(httpClient, "alex@dealer.com");
        var tcs = new TaskCompletionSource<ServiceRequestPendingPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dispatcherToken);
        connection.On<ServiceRequestPendingPayload>("ServiceRequestPending", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // A requester submits a request
        var requesterToken = await GetTokenAsync(httpClient, "gold1@example.com");
        SetBearer(httpClient, requesterToken);

        // Act
        await httpClient.PostAsJsonAsync("/service-requests",
            new { dtcId = SeedConstants.Dtc001Id, latitude = 37.7749, longitude = -122.4194 });

        // Assert
        var payload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        payload.RequestId.Should().NotBeEmpty();
        payload.RequesterTier.Should().Be("Gold");
        payload.DtcTitle.Should().Be("Hydraulic system fault");

        await connection.StopAsync();
    }
}
