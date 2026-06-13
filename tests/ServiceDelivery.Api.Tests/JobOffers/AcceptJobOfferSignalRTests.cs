using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.JobOffers;

[Collection("Hub Tests")]
public class AcceptJobOfferSignalRTests
{
    private static async Task<Guid> SeedOfferAsync(CustomWebApplicationFactory factory, Guid offerId, Guid repId)
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
            Status = ServiceRequestStatus.Pending,
            Latitude = 41.6,
            Longitude = -93.6,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.JobOffers.Add(new JobOffer
        {
            Id = offerId,
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            Status = JobOfferStatus.Pending
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        var vehicle = await db.Vehicles.FindAsync(SeedConstants.Vehicle1Id);
        if (vehicle is not null)
        {
            vehicle.ClaimedByRepId = repId;
            vehicle.LastLatitude = 41.5;
            vehicle.LastLongitude = -93.6;
        }

        await db.SaveChangesAsync();
        return requestId;
    }

    private static async Task AcceptAsRepAsync(CustomWebApplicationFactory factory, Guid offerId)
    {
        var repToken = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);
        await client.PostAsync($"/job-offers/{offerId}/accept", null);
    }

    [Fact]
    public async Task GivenAConnectedRequester_WhenRepAcceptsOffer_ThenRequesterReceivesRepAssignedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        await SeedOfferAsync(factory, offerId, SeedConstants.Rep1Id);

        var requesterToken = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepAssignedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requesterToken);
        connection.On<RepAssignedPayload>("RepAssigned", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await AcceptAsRepAsync(factory, offerId);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(SeedConstants.Rep1Id);
        received.RepName.Should().Be("Rep One");
        received.EtaMinutes.Should().BeGreaterThan(0);
        received.Latitude.Should().Be(41.5);
        received.Longitude.Should().Be(-93.6);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenAConnectedDispatcher_WhenRepAcceptsOffer_ThenDispatcherReceivesServiceRequestAssignedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        var requestId = await SeedOfferAsync(factory, offerId, SeedConstants.Rep1Id);

        var dispatcherToken = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<ServiceRequestAssignedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dispatcherToken);
        connection.On<ServiceRequestAssignedPayload>("ServiceRequestAssigned", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await AcceptAsRepAsync(factory, offerId);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RequestId.Should().Be(requestId);
        received.RepId.Should().Be(SeedConstants.Rep1Id);
        received.RepName.Should().Be("Rep One");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenAConnectedDispatcher_WhenRepAcceptsOffer_ThenDispatcherReceivesRepStateChangedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        await SeedOfferAsync(factory, offerId, SeedConstants.Rep1Id);

        var dispatcherToken = await HubTestHelpers.GetTokenAsync(factory, "alex@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepStateChangedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/dispatch", dispatcherToken);
        connection.On<RepStateChangedPayload>("RepStateChanged", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await AcceptAsRepAsync(factory, offerId);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(SeedConstants.Rep1Id);
        received.OldState.Should().Be("Available");
        received.NewState.Should().Be("EnRoute");

        await connection.StopAsync();
    }
}
