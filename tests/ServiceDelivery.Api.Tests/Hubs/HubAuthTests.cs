using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Hubs;

[Collection("Hub Tests")]
public class HubAuthTests
{
    [Theory]
    [InlineData("/hubs/position")]
    [InlineData("/hubs/dispatch")]
    [InlineData("/hubs/rep")]
    [InlineData("/hubs/requester")]
    public async Task GivenNoJWT_WhenConnectingToAnyHub_ThenConnectionIsRejected(string hubPath)
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var connection = HubTestHelpers.BuildHubConnection(factory, hubPath, bearerToken: null);

        // Act
        var act = async () => await connection.StartAsync();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GivenTwoDispatchersFromDifferentDealers_WhenEventSentToDealer1Group_ThenOnlyDealer1DispatcherReceivesIt()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var dealer1Token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);
        var dealer2Token = await HubTestHelpers.GetTokenAsync(factory, "dispatcher@dealer2.com", SeedConstants.DefaultPassword);

        var dealer1Tcs = new TaskCompletionSource<ServiceRequestPendingPayload>();
        var dealer2Tcs = new TaskCompletionSource<ServiceRequestPendingPayload>();

        var dealer1Connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dealer1Token);
        var dealer2Connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dealer2Token);

        dealer1Connection.On<ServiceRequestPendingPayload>("ServiceRequestPending", dealer1Tcs.SetResult);
        dealer2Connection.On<ServiceRequestPendingPayload>("ServiceRequestPending", dealer2Tcs.SetResult);

        await dealer1Connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(dealer1Connection);
        await dealer2Connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(dealer2Connection);

        var requestId = Guid.NewGuid();
        var payload = new ServiceRequestPendingPayload(requestId, "Bronze", "Braking system fault", "456 Elm St");
        var dealer1Group = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IDispatchHubService>();
        await hubService.SendServiceRequestPendingAsync(dealer1Group, payload);

        // Assert
        var received = await dealer1Tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(requestId);

        dealer2Tcs.Task.IsCompleted.Should().BeFalse("dealer 2 dispatcher should not receive dealer 1's event");

        await dealer1Connection.StopAsync();
        await dealer2Connection.StopAsync();
    }

    [Fact]
    public async Task GivenTwoRepConnections_WhenJobOfferSentToRep1UserGroup_ThenOnlyRep1ReceivesEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var rep1Token = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        var rep2Token = await HubTestHelpers.GetTokenAsync(factory, "rep2@dealer.com", SeedConstants.DefaultPassword);

        var rep1Tcs = new TaskCompletionSource<JobOfferReceivedPayload>();
        var rep2Tcs = new TaskCompletionSource<JobOfferReceivedPayload>();

        var rep1Connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", rep1Token);
        var rep2Connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", rep2Token);

        rep1Connection.On<JobOfferReceivedPayload>("JobOfferReceived", rep1Tcs.SetResult);
        rep2Connection.On<JobOfferReceivedPayload>("JobOfferReceived", rep2Tcs.SetResult);

        await rep1Connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(rep1Connection);
        await rep2Connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(rep2Connection);

        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var payload = new JobOfferReceivedPayload(offerId, requestId, "Gold User 1", "Gold", "Powertrain fault", 40.7128, -74.0060, 4.2, 15.0);
        var rep1Group = $"rep:{SeedConstants.Rep1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRepHubService>();
        await hubService.SendJobOfferReceivedAsync(rep1Group, payload);

        // Assert
        var received = await rep1Tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.OfferId.Should().Be(offerId);

        rep2Tcs.Task.IsCompleted.Should().BeFalse("rep2 should not receive rep1's job offer");

        await rep1Connection.StopAsync();
        await rep2Connection.StopAsync();
    }

    [Fact]
    public async Task GivenTwoRequesterConnections_WhenRepAssignedSentToRequester1UserGroup_ThenOnlyRequester1ReceivesEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requester1Token = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);
        var requester2Token = await HubTestHelpers.GetTokenAsync(factory, "bronze2@example.com", SeedConstants.DefaultPassword);

        var req1Tcs = new TaskCompletionSource<RepAssignedPayload>();
        var req2Tcs = new TaskCompletionSource<RepAssignedPayload>();

        var req1Connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requester1Token);
        var req2Connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requester2Token);

        req1Connection.On<RepAssignedPayload>("RepAssigned", req1Tcs.SetResult);
        req2Connection.On<RepAssignedPayload>("RepAssigned", req2Tcs.SetResult);

        await req1Connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(req1Connection);
        await req2Connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(req2Connection);

        var repId = SeedConstants.Rep3Id;
        var payload = new RepAssignedPayload(repId, "Rep Three", 10.0, 40.7128, -74.0060);
        var requester1Group = $"requester:{SeedConstants.Bronze1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRequesterHubService>();
        await hubService.SendRepAssignedAsync(requester1Group, payload);

        // Assert
        var received = await req1Tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(repId);

        req2Tcs.Task.IsCompleted.Should().BeFalse("requester2 should not receive requester1's RepAssigned event");

        await req1Connection.StopAsync();
        await req2Connection.StopAsync();
    }
}
