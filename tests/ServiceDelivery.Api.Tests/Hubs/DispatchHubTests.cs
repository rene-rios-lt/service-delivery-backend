using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Hubs;

[Collection("Hub Tests")]
public class DispatchHubTests
{
    [Fact]
    public async Task GivenADispatcherConnection_WhenServiceRequestPendingIsSent_ThenDispatcherReceivesServiceRequestPendingEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<ServiceRequestPendingPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", token);
        connection.On<ServiceRequestPendingPayload>("ServiceRequestPending", tcs.SetResult);
        await connection.StartAsync();

        var requestId = Guid.NewGuid();
        var payload = new ServiceRequestPendingPayload(requestId, "Gold", "Hydraulic system fault", "123 Main St");
        var dealerGroup = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IDispatchHubService>();
        await hubService.SendServiceRequestPendingAsync(dealerGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(requestId);
        received.RequesterTier.Should().Be("Gold");
        received.DtcTitle.Should().Be("Hydraulic system fault");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenADispatcherConnection_WhenServiceRequestAssignedIsSent_ThenDispatcherReceivesServiceRequestAssignedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<ServiceRequestAssignedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", token);
        connection.On<ServiceRequestAssignedPayload>("ServiceRequestAssigned", tcs.SetResult);
        await connection.StartAsync();

        var requestId = Guid.NewGuid();
        var repId = SeedConstants.Rep1Id;
        var payload = new ServiceRequestAssignedPayload(requestId, repId, "Rep One", 15.5);
        var dealerGroup = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IDispatchHubService>();
        await hubService.SendServiceRequestAssignedAsync(dealerGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(requestId);
        received.RepId.Should().Be(repId);
        received.RepName.Should().Be("Rep One");
        received.Eta.Should().Be(15.5);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenADispatcherConnection_WhenServiceRequestCompletedIsSent_ThenDispatcherReceivesServiceRequestCompletedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<ServiceRequestCompletedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", token);
        connection.On<ServiceRequestCompletedPayload>("ServiceRequestCompleted", tcs.SetResult);
        await connection.StartAsync();

        var requestId = Guid.NewGuid();
        var payload = new ServiceRequestCompletedPayload(requestId);
        var dealerGroup = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IDispatchHubService>();
        await hubService.SendServiceRequestCompletedAsync(dealerGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(requestId);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenADispatcherConnection_WhenRepStateChangedIsSent_ThenDispatcherReceivesRepStateChangedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<RepStateChangedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", token);
        connection.On<RepStateChangedPayload>("RepStateChanged", tcs.SetResult);
        await connection.StartAsync();

        var repId = SeedConstants.Rep1Id;
        var payload = new RepStateChangedPayload(repId, "Available", "EnRoute");
        var dealerGroup = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IDispatchHubService>();
        await hubService.SendRepStateChangedAsync(dealerGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(repId);
        received.OldState.Should().Be("Available");
        received.NewState.Should().Be("EnRoute");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenADispatcherConnection_WhenRepOfflineMidJobIsSent_ThenDispatcherReceivesRepOfflineMidJobEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<RepOfflineMidJobPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", token);
        connection.On<RepOfflineMidJobPayload>("RepOfflineMidJob", tcs.SetResult);
        await connection.StartAsync();

        var repId = SeedConstants.Rep1Id;
        var requestId = Guid.NewGuid();
        var payload = new RepOfflineMidJobPayload(repId, requestId);
        var dealerGroup = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IDispatchHubService>();
        await hubService.SendRepOfflineMidJobAsync(dealerGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(repId);
        received.RequestId.Should().Be(requestId);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenADispatcherConnection_WhenFleetPositionUpdateIsSent_ThenDispatcherReceivesFleetPositionUpdateEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<FleetPositionUpdatePayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", token);
        connection.On<FleetPositionUpdatePayload>("FleetPositionUpdate", tcs.SetResult);
        await connection.StartAsync();

        var repId = SeedConstants.Rep2Id;
        var payload = new FleetPositionUpdatePayload(repId, 34.0522, -118.2437, "Available");
        var dealerGroup = $"dealer:{SeedConstants.DealerId}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IDispatchHubService>();
        await hubService.SendFleetPositionUpdateAsync(dealerGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(repId);
        received.Latitude.Should().Be(34.0522);
        received.Longitude.Should().Be(-118.2437);
        received.State.Should().Be("Available");

        await connection.StopAsync();
    }
}
