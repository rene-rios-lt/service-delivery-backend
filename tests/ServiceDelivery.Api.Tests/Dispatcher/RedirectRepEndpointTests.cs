using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Dispatcher;

public class RedirectRepEndpointTests
{
    private record RedirectBody(Guid RepId, Guid ToRequestId);

    // Far from the displaced request (41.6, -93.6) so the proximity guard passes for happy-path cases.
    private const double FarVehicleLat = 40.0;
    private const double FarVehicleLng = -93.6;

    private static async Task<string> GetTokenAsync(HttpClient client, string email)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, SeedConstants.DefaultPassword));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(CustomWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<(Guid FromRequestId, Guid ToRequestId)> SeedRedirectScenarioAsync(
        CustomWebApplicationFactory factory,
        Guid repId,
        Guid vehicleId,
        ServiceTier fromTier,
        ServiceTier toTier,
        RepState repState = RepState.EnRoute,
        DateTime? lastRedirectedAt = null,
        double vehicleLat = FarVehicleLat,
        double vehicleLng = FarVehicleLng)
    {
        var fromRequestId = Guid.NewGuid();
        var toRequestId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vehicle = db.Vehicles.Single(v => v.Id == vehicleId);
        vehicle.ClaimedByRepId = repId;
        vehicle.LastLatitude = vehicleLat;
        vehicle.LastLongitude = vehicleLng;

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = repState,
            ActiveRequestId = repState == RepState.Available ? null : fromRequestId,
            LastRedirectedAt = lastRedirectedAt,
            UpdatedAt = DateTime.UtcNow
        });

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = fromRequestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Silver1Id,
            DtcId = SeedConstants.Dtc001Id,
            Latitude = 41.6,
            Longitude = -93.6,
            Tier = fromTier,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId,
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
            Tier = toTier,
            Status = ServiceRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });

        await db.SaveChangesAsync();
        return (fromRequestId, toRequestId);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenPosted_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (_, toRequestId) = await SeedRedirectScenarioAsync(
            factory, SeedConstants.Rep1Id, SeedConstants.Vehicle1Id, ServiceTier.Bronze, ServiceTier.Gold);
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, toRequestId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenAValidRedirect_WhenPosted_ThenDisplacedRequestIsPendingInDatabase()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (fromRequestId, toRequestId) = await SeedRedirectScenarioAsync(
            factory, SeedConstants.Rep1Id, SeedConstants.Vehicle1Id, ServiceTier.Bronze, ServiceTier.Gold);
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, toRequestId));

        // Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var displaced = db.ServiceRequests.Single(r => r.Id == fromRequestId);
        displaced.Status.Should().Be(ServiceRequestStatus.Pending);
        displaced.AssignedRepId.Should().BeNull();
        displaced.DisplacedFromRepId.Should().Be(SeedConstants.Rep1Id);
    }

    [Fact]
    public async Task GivenARepNotEnRoute_WhenPostingRedirect_ThenReturns422WithReason()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (_, toRequestId) = await SeedRedirectScenarioAsync(
            factory, SeedConstants.Rep1Id, SeedConstants.Vehicle1Id, ServiceTier.Bronze, ServiceTier.Gold,
            repState: RepState.Available);
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, toRequestId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ReasonResponse>();
        body!.Reason.Should().Be("RepNotEnRoute");
    }

    [Fact]
    public async Task GivenAnEqualTier_WhenPostingRedirect_ThenReturns422WithTierNotHigher()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (_, toRequestId) = await SeedRedirectScenarioAsync(
            factory, SeedConstants.Rep1Id, SeedConstants.Vehicle1Id, ServiceTier.Silver, ServiceTier.Silver);
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, toRequestId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ReasonResponse>();
        body!.Reason.Should().Be("TierNotHigher");
    }

    [Fact]
    public async Task GivenASilverRedirectDuringCooldown_WhenPostingRedirect_ThenReturns422WithCooldownActive()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (_, toRequestId) = await SeedRedirectScenarioAsync(
            factory, SeedConstants.Rep1Id, SeedConstants.Vehicle1Id, ServiceTier.Bronze, ServiceTier.Silver,
            lastRedirectedAt: DateTime.UtcNow.AddMinutes(-2));
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, toRequestId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ReasonResponse>();
        body!.Reason.Should().Be("CooldownActive");
    }

    [Fact]
    public async Task GivenAnUnknownTargetRequest_WhenPostingRedirect_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedRedirectScenarioAsync(
            factory, SeedConstants.Rep1Id, SeedConstants.Vehicle1Id, ServiceTier.Bronze, ServiceTier.Gold);
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenAServiceRepToken_WhenPostingRedirect_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "rep1@dealer.com");

        // Act
        var response = await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoToken_WhenPostingRedirect_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // BUG-059 AC-3: a Within15Miles rep whose vehicle moves back out past the threshold becomes redirect-eligible again.
    private record PositionBody(double Latitude, double Longitude, DateTime Timestamp);

    [Fact]
    public async Task GivenAWithin15MilesRepWhoseVehicleMovesOutPast15Miles_WhenRedirectPosted_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (_, toRequestId) = await SeedRedirectScenarioAsync(
            factory, SeedConstants.Rep1Id, SeedConstants.Vehicle1Id, ServiceTier.Bronze, ServiceTier.Gold,
            repState: RepState.Within15Miles,
            vehicleLat: FarVehicleLat, vehicleLng: FarVehicleLng);
        var simulatorClient = await CreateAuthenticatedClientAsync(factory, "simulator@system.internal");
        var dispatcherClient = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act — step 1: a position update far from the displaced request transitions the rep Within15Miles → EnRoute
        var positionResponse = await simulatorClient.PostAsJsonAsync(
            $"/vehicles/{SeedConstants.Vehicle1Id}/position",
            new PositionBody(FarVehicleLat, FarVehicleLng, DateTime.UtcNow));
        positionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — step 2: the redirect now succeeds (state gate and proximity guard both clear)
        var response = await dispatcherClient.PostAsJsonAsync(
            "/dispatcher/redirect", new RedirectBody(SeedConstants.Rep1Id, toRequestId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record ReasonResponse(string Reason);
}
