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

public class GetDispatcherFleetEndpointTests
{
    private record DispatcherFleetEntryResponse(
        Guid RepId,
        string? Name,
        string State,
        Guid VehicleId,
        string Registration,
        LastPositionResponse? LastPosition,
        Guid? ActiveRequestId,
        string? ActiveRequestTier,
        bool HumanControlled);

    private record LastPositionResponse(double Lat, double Lng);

    private static async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        CustomWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, email, SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task ClaimVehicleWithActiveRequestAsync(
        CustomWebApplicationFactory factory,
        Guid vehicleId,
        Guid repId,
        RepState state,
        bool humanControlled,
        Guid requestId,
        double vehicleLat,
        double vehicleLng)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vehicle = db.Vehicles.Single(v => v.Id == vehicleId);
        vehicle.ClaimedByRepId = repId;
        vehicle.LastLatitude = vehicleLat;
        vehicle.LastLongitude = vehicleLng;

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = state,
            HumanControlled = humanControlled,
            ActiveRequestId = requestId,
            UpdatedAt = DateTime.UtcNow
        });
        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Gold1Id,
            DtcId = SeedConstants.Dtc001Id,
            Latitude = 0,
            Longitude = 0,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenGetFleetCalled_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.GetAsync("/dispatcher/fleet");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenAServiceRepToken_WhenGetFleetCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "rep1@dealer.com");

        // Act
        var response = await client.GetAsync("/dispatcher/fleet");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenARequesterToken_WhenGetFleetCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "bronze1@example.com");

        // Act
        var response = await client.GetAsync("/dispatcher/fleet");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenASimulatorToken_WhenGetFleetCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "simulator@system.internal");

        // Act
        var response = await client.GetAsync("/dispatcher/fleet");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetFleetCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/dispatcher/fleet");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenGetFleetCalled_ThenOnlyOwnDealerEntriesReturned()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.GetAsync("/dispatcher/fleet");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DispatcherFleetEntryResponse[]>();
        body.Should().NotBeNullOrEmpty();
        body.Should().Contain(v => v.VehicleId == SeedConstants.Vehicle1Id);
        body.Should().NotContain(v => v.VehicleId == SeedConstants.Dealer2Vehicle1Id);
    }

    [Fact]
    public async Task GivenSeededClaimedRepWithActiveRequest_WhenGetFleetCalledAsDispatcher_ThenResponseContainsAllRequiredFields()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await ClaimVehicleWithActiveRequestAsync(
            factory, SeedConstants.Vehicle1Id, SeedConstants.Rep1Id,
            RepState.EnRoute, true, requestId, 41.5, -93.6);
        var client = await CreateAuthenticatedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.GetAsync("/dispatcher/fleet");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DispatcherFleetEntryResponse[]>();
        body.Should().NotBeNullOrEmpty();
        var dto = body!.First(v => v.VehicleId == SeedConstants.Vehicle1Id);
        dto.RepId.Should().Be(SeedConstants.Rep1Id);
        dto.Name.Should().NotBeNullOrEmpty();
        dto.State.Should().Be("EnRoute");
        dto.Registration.Should().NotBeNullOrEmpty();
        dto.LastPosition.Should().NotBeNull();
        dto.LastPosition!.Lat.Should().Be(41.5);
        dto.LastPosition.Lng.Should().Be(-93.6);
        dto.ActiveRequestId.Should().Be(requestId);
        dto.ActiveRequestTier.Should().Be("Gold");
        dto.HumanControlled.Should().BeTrue();
    }
}
