using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Simulator;

public class GetFleetStateEndpointTests
{
    private record FleetStateVehicleResponse(
        Guid VehicleId,
        Guid? ClaimingRepId,
        string RepState,
        bool HumanControlled,
        ActiveRequestLocationResponse? ActiveRequestLocation);

    private record ActiveRequestLocationResponse(double Lat, double Lng);

    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private static async Task ClaimVehicleWithActiveRequestAsync(
        CustomWebApplicationFactory factory,
        Guid vehicleId,
        Guid repId,
        RepState state,
        bool humanControlled,
        Guid requestId,
        double latitude,
        double longitude)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vehicle = db.Vehicles.Single(v => v.Id == vehicleId);
        vehicle.ClaimedByRepId = repId;

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
            Latitude = latitude,
            Longitude = longitude,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<HttpClient> CreateSimulatorClientAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login",
            new LoginCommand("simulator@system.internal", SeedConstants.DefaultPassword));
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result!.Token);
        return client;
    }

    [Fact]
    public async Task GivenSeededFleetJobState_WhenGetFleetStateCalledAsSimulator_ThenResponseContainsAllRequiredFields()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await ClaimVehicleWithActiveRequestAsync(
            factory, SeedConstants.Vehicle1Id, SeedConstants.Rep1Id,
            RepState.EnRoute, true, requestId, 41.5, -93.6);
        var client = await CreateSimulatorClientAsync(factory);

        // Act
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FleetStateVehicleResponse[]>();
        body.Should().NotBeNullOrEmpty();
        var dto = body!.First(v => v.VehicleId == SeedConstants.Vehicle1Id);
        dto.ClaimingRepId.Should().Be(SeedConstants.Rep1Id);
        dto.RepState.Should().Be("EnRoute");
        dto.HumanControlled.Should().BeTrue();
        dto.ActiveRequestLocation.Should().NotBeNull();
        dto.ActiveRequestLocation!.Lat.Should().Be(41.5);
        dto.ActiveRequestLocation.Lng.Should().Be(-93.6);
    }

    [Fact]
    public async Task GivenASimulatorToken_WhenGetFleetStateCalled_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateSimulatorClientAsync(factory);

        // Act
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenAServiceRepToken_WhenGetFleetStateCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenGetFleetStateCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenARequesterToken_WhenGetFleetStateCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "bronze1@example.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetFleetStateCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenVehiclesInTwoDealers_WhenGetFleetStateCalledAsSimulator_ThenOnlyOwnDealerVehiclesReturned()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateSimulatorClientAsync(factory);

        // Act
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FleetStateVehicleResponse[]>();
        body.Should().NotBeNullOrEmpty();
        body.Should().Contain(v => v.VehicleId == SeedConstants.Vehicle1Id);
        body.Should().NotContain(v => v.VehicleId == SeedConstants.Dealer2Vehicle1Id);
    }

    [Fact]
    public async Task GivenPersistedRepStateAndRequest_WhenGetFleetStateCalled_ThenResponseReflectsPersistedValues()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await ClaimVehicleWithActiveRequestAsync(
            factory, SeedConstants.Vehicle2Id, SeedConstants.Rep2Id,
            RepState.OnSite, false, requestId, 30.25, -97.75);
        var client = await CreateSimulatorClientAsync(factory);

        // Act
        var response = await client.GetAsync("/simulator/fleet-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FleetStateVehicleResponse[]>();
        var dto = body!.First(v => v.VehicleId == SeedConstants.Vehicle2Id);
        dto.ClaimingRepId.Should().Be(SeedConstants.Rep2Id);
        dto.RepState.Should().Be("OnSite");
        dto.HumanControlled.Should().BeFalse();
        dto.ActiveRequestLocation!.Lat.Should().Be(30.25);
        dto.ActiveRequestLocation.Lng.Should().Be(-97.75);
    }

    [Fact]
    public async Task GivenASimulatorToken_WhenPostToFleetStateCalled_ThenReturns405()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await CreateSimulatorClientAsync(factory);

        // Act
        var response = await client.PostAsync("/simulator/fleet-state", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
