using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class ClaimVehicleEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    // Happy path — 200 on successful claim
    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenClaimEndpointCalledByAuthenticatedRep_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-3 — 409 if vehicle is already claimed
    [Fact]
    public async Task GivenAnAlreadyClaimedVehicle_WhenClaimEndpointCalled_ThenReturns409()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Rep1 claims the vehicle first
        var rep1Token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rep1Token);
        await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Rep2 tries to claim the same vehicle
        var rep2Token = await GetTokenAsync(client, "rep2@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rep2Token);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // AC-4 — 409 if rep already has an active session on another vehicle
    [Fact]
    public async Task GivenARepWithAnActiveSession_WhenClaimEndpointCalledForAnotherVehicle_ThenReturns409()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Rep1 claims vehicle 1 first
        await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Act — same rep tries to claim vehicle 2
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle2Id}/claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Not found — 404 if vehicle ID does not exist
    [Fact]
    public async Task GivenANonExistentVehicleId_WhenClaimEndpointCalled_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/vehicles/{nonExistentId}/claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Auth — 401 if unauthenticated
    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenClaimVehicleCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Auth — 403 if non-ServiceRep role
    [Fact]
    public async Task GivenADispatcherToken_WhenClaimVehicleCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
