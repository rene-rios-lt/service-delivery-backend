using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Hubs;

public class RepHubTests
{
    [Fact]
    public async Task GivenARepConnection_WhenJobOfferReceivedIsSentToThatRep_ThenRepReceivesJobOfferReceivedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<JobOfferReceivedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", token);
        connection.On<JobOfferReceivedPayload>("JobOfferReceived", tcs.SetResult);
        await connection.StartAsync();

        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var payload = new JobOfferReceivedPayload(offerId, requestId, "Gold User 1", "Gold", "Hydraulic system fault", 40.7128, -74.0060, 3.5, 12.0);
        var repGroup = $"rep:{SeedConstants.Rep1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRepHubService>();
        await hubService.SendJobOfferReceivedAsync(repGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.OfferId.Should().Be(offerId);
        received.RequestId.Should().Be(requestId);
        received.RequesterTier.Should().Be("Gold");
        received.DtcTitle.Should().Be("Hydraulic system fault");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenARepConnection_WhenJobOfferExpiredIsSentToThatRep_ThenRepReceivesJobOfferExpiredEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<JobOfferExpiredPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", token);
        connection.On<JobOfferExpiredPayload>("JobOfferExpired", tcs.SetResult);
        await connection.StartAsync();

        var offerId = Guid.NewGuid();
        var payload = new JobOfferExpiredPayload(offerId);
        var repGroup = $"rep:{SeedConstants.Rep1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRepHubService>();
        await hubService.SendJobOfferExpiredAsync(repGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.OfferId.Should().Be(offerId);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenARepConnection_WhenRedirectReceivedIsSentToThatRep_ThenRepReceivesRedirectReceivedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<RedirectReceivedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", token);
        connection.On<RedirectReceivedPayload>("RedirectReceived", tcs.SetResult);
        await connection.StartAsync();

        var newRequestId = Guid.NewGuid();
        var payload = new RedirectReceivedPayload(newRequestId, "Silver User 1", "Silver", "Electrical system fault", 34.0522, -118.2437, 5.2, 18.0);
        var repGroup = $"rep:{SeedConstants.Rep1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRepHubService>();
        await hubService.SendRedirectReceivedAsync(repGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.NewRequestId.Should().Be(newRequestId);
        received.RequesterName.Should().Be("Silver User 1");
        received.RequesterTier.Should().Be("Silver");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenSimulatorJWT_WhenConnectingToRepHub_ThenConnectionSucceedsAndSimulatorReceivesJobOfferReceivedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "simulator@system.internal", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<JobOfferReceivedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", token);
        connection.On<JobOfferReceivedPayload>("JobOfferReceived", tcs.SetResult);
        await connection.StartAsync();

        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var payload = new JobOfferReceivedPayload(offerId, requestId, "Bronze User 1", "Bronze", "Transmission fault", 37.7749, -122.4194, 2.1, 8.0);
        var simulatorGroup = $"rep:{SeedConstants.SimulatorId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRepHubService>();
        await hubService.SendJobOfferReceivedAsync(simulatorGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.OfferId.Should().Be(offerId);
        received.DtcTitle.Should().Be("Transmission fault");

        await connection.StopAsync();
    }
}
