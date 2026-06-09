using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.Vehicles.Queries;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class VehiclesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VehiclesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    [Fact]
    public async Task GivenADispatcher_WhenGetVehiclesCalled_ThenOnlyDealerVehiclesAreReturned()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var knownDealer1VehicleIds = new[]
        {
            SeedConstants.Vehicle1Id,
            SeedConstants.Vehicle2Id,
            SeedConstants.Vehicle3Id,
            SeedConstants.Vehicle4Id,
            SeedConstants.Vehicle5Id,
            SeedConstants.Vehicle6Id,
            SeedConstants.Vehicle7Id,
            SeedConstants.Vehicle8Id,
        };

        // Act
        var response = await client.GetAsync("/vehicles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicles = await response.Content.ReadFromJsonAsync<VehicleDto[]>();
        vehicles.Should().HaveCount(8);
        var returnedIds = vehicles!.Select(v => v.VehicleId).ToList();
        returnedIds.Should().BeEquivalentTo(knownDealer1VehicleIds);
        returnedIds.Should().NotContain(SeedConstants.Dealer2Vehicle1Id);
    }

    [Fact]
    public async Task GivenADispatcher_WhenGetVehiclesCalled_ThenReturns200WithCorrectShape()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/vehicles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicles = await response.Content.ReadFromJsonAsync<VehicleDto[]>();
        vehicles.Should().NotBeEmpty();

        var spotCheck = vehicles!.FirstOrDefault(v => v.VehicleId == SeedConstants.Vehicle1Id);
        spotCheck.Should().NotBeNull();
        spotCheck!.Registration.Should().Be("V-001");
        spotCheck.State.Should().BeOneOf("Unclaimed", "Claimed");
        spotCheck.Equipment.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GivenAServiceRepToken_WhenGetVehiclesCalled_ThenReturns403()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/vehicles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenARequesterToken_WhenGetVehiclesCalled_ThenReturns403()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "bronze1@example.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/vehicles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetVehiclesCalled_ThenReturns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/vehicles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
