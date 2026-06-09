using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class UpdateVehiclePositionEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    // AC-1 integration: 200 on successful position update
    [Fact]
    public async Task GivenASimulatorToken_WhenPostingPosition_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "simulator@system.internal", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            latitude = 51.5074,
            longitude = -0.1278,
            timestamp = DateTime.UtcNow
        };

        // Act
        var response = await client.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-7: Requires Simulator role — 403 for non-Simulator token
    [Fact]
    public async Task GivenANonSimulatorToken_WhenPostingVehiclePosition_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            latitude = 51.5074,
            longitude = -0.1278,
            timestamp = DateTime.UtcNow
        };

        // Act
        var response = await client.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Vehicle not found — 404
    [Fact]
    public async Task GivenANonExistentVehicleId_WhenPostingPosition_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "simulator@system.internal", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            latitude = 51.5074,
            longitude = -0.1278,
            timestamp = DateTime.UtcNow
        };

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync($"/vehicles/{nonExistentId}/position", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Auth — 401 if unauthenticated
    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenPostingVehiclePosition_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var body = new
        {
            latitude = 51.5074,
            longitude = -0.1278,
            timestamp = DateTime.UtcNow
        };

        // Act
        var response = await client.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
