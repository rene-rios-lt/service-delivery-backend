using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Rep;

[Collection("Hub Tests")]
public class ArriveSignalRTests
{
    private static async Task<Guid> SeedArrivalScenarioAsync(CustomWebApplicationFactory factory, Guid repId)
    {
        var requestId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Bronze1Id,
            DtcId = SeedConstants.Dtc001Id,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId,
            Latitude = 41.6,
            Longitude = -93.6,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Within15Miles,
            ActiveRequestId = requestId,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();
        return requestId;
    }

    private static async Task ArriveAsRepAsync(CustomWebApplicationFactory factory)
    {
        var repToken = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);
        await client.PostAsync("/rep/arrive", null);
    }

    [Fact]
    public async Task GivenAConnectedDispatcher_WhenRepArrives_ThenReceivesRepStateChangedWithOnSite()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedArrivalScenarioAsync(factory, SeedConstants.Rep1Id);

        var dispatcherToken = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepStateChangedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dispatcherToken);
        connection.On<RepStateChangedPayload>("RepStateChanged", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await ArriveAsRepAsync(factory);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(SeedConstants.Rep1Id);
        received.OldState.Should().Be("Within15Miles");
        received.NewState.Should().Be("OnSite");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenAConnectedRequester_WhenRepArrives_ThenReceivesRepArrivedWithRepIdAndRequestId()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = await SeedArrivalScenarioAsync(factory, SeedConstants.Rep1Id);

        var requesterToken = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepArrivedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requesterToken);
        connection.On<RepArrivedPayload>("RepArrived", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await ArriveAsRepAsync(factory);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(SeedConstants.Rep1Id);
        received.RequestId.Should().Be(requestId);

        await connection.StopAsync();
    }
}
