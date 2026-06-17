using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Hubs;

[Collection("Hub Tests")]
public class RepOfflineMidJobTests
{
    [Fact]
    public async Task GivenADispatcherConnection_WhenRepGoesOfflineMidJob_ThenDispatcherReceivesRepOfflineMidJobWithRepNameAndDtcTitle()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<RepOfflineMidJobPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", token);
        connection.On<RepOfflineMidJobPayload>("RepOfflineMidJob", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        var repId = SeedConstants.Rep1Id;
        var requestId = Guid.NewGuid();
        var payload = new RepOfflineMidJobPayload(repId, requestId, "Rep One", "Hydraulic system fault");
        var dealerGroup = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IDispatchHubService>();
        await hubService.SendRepOfflineMidJobAsync(dealerGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(repId);
        received.RequestId.Should().Be(requestId);
        received.RepName.Should().Be("Rep One");
        received.DtcTitle.Should().Be("Hydraulic system fault");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenARequesterConnection_WhenTheirRepGoesOfflineMidJob_ThenRequesterReceivesRequestBackToPending()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<RequestBackToPendingPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", token);
        connection.On<RequestBackToPendingPayload>("RequestBackToPending", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        var requestId = Guid.NewGuid();
        var payload = new RequestBackToPendingPayload(requestId);
        var requesterGroup = $"requester:{SeedConstants.Bronze1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRequesterHubService>();
        await hubService.SendRequestBackToPendingAsync(requesterGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(requestId);

        await connection.StopAsync();
    }
}
