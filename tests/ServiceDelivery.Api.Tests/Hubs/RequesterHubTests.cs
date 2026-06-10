using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Hubs;

[Collection("Hub Tests")]
public class RequesterHubTests
{
    [Fact]
    public async Task GivenARequesterConnection_WhenRepAssignedIsSentToThatRequester_ThenRequesterReceivesRepAssignedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<RepAssignedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", token);
        connection.On<RepAssignedPayload>("RepAssigned", tcs.SetResult);
        await connection.StartAsync();

        var repId = SeedConstants.Rep1Id;
        var payload = new RepAssignedPayload(repId, "Rep One", 12.5, 40.7128, -74.0060);
        var requesterGroup = $"requester:{SeedConstants.Bronze1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRequesterHubService>();
        await hubService.SendRepAssignedAsync(requesterGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(repId);
        received.RepName.Should().Be("Rep One");
        received.EtaMinutes.Should().Be(12.5);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenARequesterConnection_WhenRepPositionUpdatedIsSentToThatRequester_ThenRequesterReceivesRepPositionUpdatedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<RepPositionUpdatedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", token);
        connection.On<RepPositionUpdatedPayload>("RepPositionUpdated", tcs.SetResult);
        await connection.StartAsync();

        var payload = new RepPositionUpdatedPayload(40.7580, -73.9855, 8.0, "EnRoute");
        var requesterGroup = $"requester:{SeedConstants.Bronze1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRequesterHubService>();
        await hubService.SendRepPositionUpdatedAsync(requesterGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Latitude.Should().Be(40.7580);
        received.Longitude.Should().Be(-73.9855);
        received.EtaMinutes.Should().Be(8.0);
        received.State.Should().Be("EnRoute");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenARequesterConnection_WhenRepRedirectedIsSentToThatRequester_ThenRequesterReceivesRepRedirectedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<RepRedirectedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", token);
        connection.On<RepRedirectedPayload>("RepRedirected", tcs.SetResult);
        await connection.StartAsync();

        var payload = new RepRedirectedPayload("Rep One", "Rep Two", 20.0);
        var requesterGroup = $"requester:{SeedConstants.Bronze1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRequesterHubService>();
        await hubService.SendRepRedirectedAsync(requesterGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.OldRepName.Should().Be("Rep One");
        received.NewRepName.Should().Be("Rep Two");
        received.NewEtaMinutes.Should().Be(20.0);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenARequesterConnection_WhenServiceCompletedIsSentToThatRequester_ThenRequesterReceivesServiceCompletedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);

        var tcs = new TaskCompletionSource<ServiceCompletedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", token);
        connection.On<ServiceCompletedPayload>("ServiceCompleted", tcs.SetResult);
        await connection.StartAsync();

        var payload = new ServiceCompletedPayload();
        var requesterGroup = $"requester:{SeedConstants.Bronze1Id}";

        // Act
        using var scope = factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IRequesterHubService>();
        await hubService.SendServiceCompletedAsync(requesterGroup, payload);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().NotBeNull();

        await connection.StopAsync();
    }
}
