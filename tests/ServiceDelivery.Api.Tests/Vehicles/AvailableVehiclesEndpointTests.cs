using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.Vehicles.Queries;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class AvailableVehiclesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AvailableVehiclesEndpointTests(CustomWebApplicationFactory factory)
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
    public async Task GivenVehiclesAcrossTwoDealers_WhenGetAvailableVehiclesCalled_ThenOnlyRepsDealerVehiclesReturned()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
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
        var response = await client.GetAsync("/vehicles/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicles = await response.Content.ReadFromJsonAsync<AvailableVehicleDto[]>();
        vehicles.Should().NotBeNull();
        var returnedIds = vehicles!.Select(v => v.VehicleId).ToList();
        returnedIds.Should().OnlyContain(id => knownDealer1VehicleIds.Contains(id));
        returnedIds.Should().NotContain(SeedConstants.Dealer2Vehicle1Id);
    }

    [Fact]
    public async Task GivenAServiceRep_WhenGetAvailableVehiclesCalled_ThenReturns200WithCorrectShape()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/vehicles/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicles = await response.Content.ReadFromJsonAsync<AvailableVehicleDto[]>();
        vehicles.Should().NotBeNull();
        vehicles.Should().NotBeEmpty();

        var spotCheck = vehicles!.First();
        spotCheck.VehicleId.Should().NotBeEmpty();
        spotCheck.Registration.Should().NotBeNullOrEmpty();
        spotCheck.Model.Should().NotBeNullOrEmpty();
        spotCheck.Equipment.Should().NotBeNull();
    }

    [Fact]
    public async Task GivenSeededVehicles_WhenGetAvailableVehiclesCalled_ThenResponseIncludesNonNullModel()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/vehicles/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicles = await response.Content.ReadFromJsonAsync<AvailableVehicleDto[]>();
        vehicles.Should().NotBeNull();
        vehicles.Should().NotBeEmpty();
        vehicles!.Should().AllSatisfy(v => v.Model.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenGetAvailableVehiclesCalled_ThenReturns403()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/vehicles/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenARequesterToken_WhenGetAvailableVehiclesCalled_ThenReturns403()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "bronze1@example.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/vehicles/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetAvailableVehiclesCalled_ThenReturns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/vehicles/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
