using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Dispatcher;

[Collection("Hub Tests")]
public class RedirectRepSignalRTests
{
    private record RedirectBody(Guid RepId, Guid ToRequestId);

    // The displaced (EnRoute) rep's vehicle is placed far from its current job so the proximity guard passes.
    private const double FarVehicleLat = 40.0;
    private const double FarVehicleLng = -93.6;

    // The displaced request location; a second available rep is parked near it so matching picks that rep.
    private const double DisplacedLat = 41.6;
    private const double DisplacedLng = -93.6;

    private static async Task<(Guid FromRequestId, Guid ToRequestId)> SeedRedirectScenarioAsync(
        CustomWebApplicationFactory factory)
    {
        var fromRequestId = Guid.NewGuid();
        var toRequestId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Redirected rep (rep1) — EnRoute on a Silver job, vehicle far from the job.
        var vehicle1 = db.Vehicles.Single(v => v.Id == SeedConstants.Vehicle1Id);
        vehicle1.ClaimedByRepId = SeedConstants.Rep1Id;
        vehicle1.LastLatitude = FarVehicleLat;
        vehicle1.LastLongitude = FarVehicleLng;

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = SeedConstants.Rep1Id,
            State = RepState.EnRoute,
            ActiveRequestId = fromRequestId,
            UpdatedAt = DateTime.UtcNow
        });

        // Second rep (rep2) — Available, vehicle parked on top of the displaced request so matching picks it.
        var vehicle2 = db.Vehicles.Single(v => v.Id == SeedConstants.Vehicle2Id);
        vehicle2.ClaimedByRepId = SeedConstants.Rep2Id;
        vehicle2.LastLatitude = DisplacedLat;
        vehicle2.LastLongitude = DisplacedLng;

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = SeedConstants.Rep2Id,
            State = RepState.Available,
            UpdatedAt = DateTime.UtcNow
        });

        // Matching projects Available candidates via an open RepSession linking the rep to its vehicle.
        db.RepSessions.Add(new RepSession
        {
            Id = Guid.NewGuid(),
            RepId = SeedConstants.Rep2Id,
            VehicleId = SeedConstants.Vehicle2Id,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            EndedAt = null
        });

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = fromRequestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Silver1Id,
            DtcId = SeedConstants.Dtc001Id,
            Latitude = DisplacedLat,
            Longitude = DisplacedLng,
            Tier = ServiceTier.Silver,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = SeedConstants.Rep1Id,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = toRequestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Gold1Id,
            DtcId = SeedConstants.Dtc001Id,
            Latitude = 42.0,
            Longitude = -94.0,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });

        await db.SaveChangesAsync();
        return (fromRequestId, toRequestId);
    }

    private static async Task<HttpClient> AuthenticatedClientAsync(CustomWebApplicationFactory factory, string email)
    {
        var token = await HubTestHelpers.GetTokenAsync(factory, email, SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task RedirectAsDispatcherAsync(CustomWebApplicationFactory factory, Guid repId, Guid toRequestId)
    {
        var client = await AuthenticatedClientAsync(factory, "alex@dealer.com");
        await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(repId, toRequestId));
    }

    [Fact]
    public async Task GivenAConnectedRep_WhenRedirected_ThenRepReceivesRedirectReceivedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (_, toRequestId) = await SeedRedirectScenarioAsync(factory);

        var repToken = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RedirectReceivedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", repToken);
        connection.On<RedirectReceivedPayload>("RedirectReceived", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await RedirectAsDispatcherAsync(factory, SeedConstants.Rep1Id, toRequestId);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.NewRequestId.Should().Be(toRequestId);
        received.RequesterName.Should().Be("Gold User 1");
        received.RequesterTier.Should().Be("Gold");
        received.DtcTitle.Should().Be("Hydraulic system fault");
        received.EtaMinutes.Should().BeGreaterThan(0);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenAConnectedNewRequester_WhenRedirected_ThenRequesterReceivesRepAssignedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (_, toRequestId) = await SeedRedirectScenarioAsync(factory);

        var requesterToken = await HubTestHelpers.GetTokenAsync(factory, "gold1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepAssignedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requesterToken);
        connection.On<RepAssignedPayload>("RepAssigned", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await RedirectAsDispatcherAsync(factory, SeedConstants.Rep1Id, toRequestId);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.RepId.Should().Be(SeedConstants.Rep1Id);
        received.RepName.Should().Be("Rep One");

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenADisplacedRequesterConnected_WhenTheNewRepAcceptsTheDisplacedRequest_ThenRequesterReceivesRepRedirectedEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (fromRequestId, toRequestId) = await SeedRedirectScenarioAsync(factory);

        var displacedRequesterToken = await HubTestHelpers.GetTokenAsync(factory, "silver1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepRedirectedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", displacedRequesterToken);
        connection.On<RepRedirectedPayload>("RepRedirected", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act — redirect rep1 to the Gold request; matching re-offers the displaced request to rep2, who accepts.
        await RedirectAsDispatcherAsync(factory, SeedConstants.Rep1Id, toRequestId);
        var offerId = await GetPendingOfferIdAsync(factory, SeedConstants.Rep2Id, fromRequestId);
        await AcceptAsRepAsync(factory, "rep2@dealer.com", offerId);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.OldRepName.Should().Be("Rep One");
        received.NewRepName.Should().Be("Rep Two");
        received.NewEtaMinutes.Should().BeGreaterThanOrEqualTo(0);

        await connection.StopAsync();
    }

    [Fact]
    public async Task GivenADisplacedRequesterConnected_WhenRedirected_ThenNoRepRedirectedEventArrivesBeforeAccept()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (_, toRequestId) = await SeedRedirectScenarioAsync(factory);

        var displacedRequesterToken = await HubTestHelpers.GetTokenAsync(factory, "silver1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<RepRedirectedPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", displacedRequesterToken);
        connection.On<RepRedirectedPayload>("RepRedirected", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act — only the redirect happens; the displaced request has not been accepted by a new rep yet.
        await RedirectAsDispatcherAsync(factory, SeedConstants.Rep1Id, toRequestId);

        // Assert — RepRedirected must NOT fire synchronously from the redirect endpoint.
        var arrived = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        arrived.Should().NotBe(tcs.Task);

        await connection.StopAsync();
    }

    private static Task<Guid> GetPendingOfferIdAsync(CustomWebApplicationFactory factory, Guid repId, Guid requestId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var offer = db.JobOffers.Single(o => o.RepId == repId
                                              && o.ServiceRequestId == requestId
                                              && o.Status == JobOfferStatus.Pending);
        return Task.FromResult(offer.Id);
    }

    private static async Task AcceptAsRepAsync(CustomWebApplicationFactory factory, string repEmail, Guid offerId)
    {
        var client = await AuthenticatedClientAsync(factory, repEmail);
        await client.PostAsync($"/job-offers/{offerId}/accept", null);
    }
}
