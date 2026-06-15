using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Rep;

[Collection("Hub Tests")]
public class CompleteSignalRTests
{
    private static async Task<Guid> SeedCompletionScenarioAsync(CustomWebApplicationFactory factory, Guid repId)
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
            Status = ServiceRequestStatus.InProgress,
            AssignedRepId = repId,
            Latitude = 41.6,
            Longitude = -93.6,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.OnSite,
            ActiveRequestId = requestId,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();
        return requestId;
    }

    private static async Task CompleteAsRepAsync(CustomWebApplicationFactory factory)
    {
        var repToken = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);
        await client.PostAsync("/rep/complete", null);
    }

    [Fact]
    public async Task GivenAConnectedRequester_WhenRepCompletes_ThenReceivesServiceCompletedWithRequestId()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = await SeedCompletionScenarioAsync(factory, SeedConstants.Rep1Id);

        var requesterToken = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<ServiceCompletedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requesterToken);
        connection.On<ServiceCompletedPayload>("ServiceCompleted", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await CompleteAsRepAsync(factory);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(requestId);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenAConnectedDispatcher_WhenRepCompletes_ThenReceivesRepStateChangedWithAvailable()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedCompletionScenarioAsync(factory, SeedConstants.Rep1Id);

        var dispatcherToken = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepStateChangedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dispatcherToken);
        connection.On<RepStateChangedPayload>("RepStateChanged", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await CompleteAsRepAsync(factory);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(SeedConstants.Rep1Id);
        received.OldState.Should().Be("OnSite");
        received.NewState.Should().Be("Available");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenAConnectedDispatcher_WhenRepCompletes_ThenReceivesServiceRequestCompletedWithRequestId()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = await SeedCompletionScenarioAsync(factory, SeedConstants.Rep1Id);

        var dispatcherToken = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<ServiceRequestCompletedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dispatcherToken);
        connection.On<ServiceRequestCompletedPayload>("ServiceRequestCompleted", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await CompleteAsRepAsync(factory);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(requestId);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenAPendingRequestAndNewlyAvailableRep_WhenCompletePosted_ThenPendingRequestReceivesAnOffer()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        const double lat = 37.7749;
        const double lon = -122.4194;

        // Rep1 claims a DTC-001-capable vehicle (Vehicle1 carries HydraulicTool) and the
        // simulator gives it a position, so Rep1 is a valid matching candidate once free.
        var repHttp = factory.CreateClient();
        var repToken = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        repHttp.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);
        await repHttp.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        var simHttp = factory.CreateClient();
        var simToken = await HubTestHelpers.GetTokenAsync(factory, "simulator@system.internal", SeedConstants.DefaultPassword);
        simHttp.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", simToken);
        await simHttp.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position",
            new { latitude = lat, longitude = lon, timestamp = DateTime.UtcNow });

        // Rep1 is mid-job (OnSite/InProgress) and a separate Pending DTC-001 request is
        // waiting with no other candidate available.
        var pendingRequestId = Guid.NewGuid();
        var inProgressRequestId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = inProgressRequestId,
                DealerId = SeedConstants.DealerId,
                RequesterId = SeedConstants.Bronze1Id,
                DtcId = SeedConstants.Dtc001Id,
                Tier = ServiceTier.Gold,
                Status = ServiceRequestStatus.InProgress,
                AssignedRepId = SeedConstants.Rep1Id,
                Latitude = lat,
                Longitude = lon,
                CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
            });

            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = pendingRequestId,
                DealerId = SeedConstants.DealerId,
                RequesterId = SeedConstants.Bronze2Id,
                DtcId = SeedConstants.Dtc001Id,
                Tier = ServiceTier.Gold,
                Status = ServiceRequestStatus.Pending,
                Latitude = lat,
                Longitude = lon,
                CreatedAt = new DateTime(2026, 6, 13, 9, 5, 0, DateTimeKind.Utc)
            });

            var rep1State = await db.RepStateRecords.FirstAsync(s => s.RepId == SeedConstants.Rep1Id);
            rep1State.State = RepState.OnSite;
            rep1State.ActiveRequestId = inProgressRequestId;

            await db.SaveChangesAsync();
        }

        // Rep1 listens on the RepHub for a re-match offer.
        var tcs = new TaskCompletionSource<JobOfferReceivedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", repToken);
        connection.On<JobOfferReceivedPayload>("JobOfferReceived", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await repHttp.PostAsync("/rep/complete", null);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(pendingRequestId);

        await connection.StopAsync();
    }
}
