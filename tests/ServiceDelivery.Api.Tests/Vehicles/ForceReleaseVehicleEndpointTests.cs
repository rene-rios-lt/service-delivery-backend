using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Vehicles;

public class ForceReleaseVehicleEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private async Task ClaimVehicleAsRepAsync(HttpClient client, string repEmail, Guid vehicleId)
    {
        var token = await GetTokenAsync(client, repEmail, SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await client.PostAsync($"/vehicles/{vehicleId}/claim", null);
    }

    private async Task SetDispatcherTokenAsync(HttpClient client)
    {
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    // AC-1 (integration): 404 when vehicle does not exist
    [Fact]
    public async Task GivenANonExistentVehicleId_WhenForceReleaseEndpointCalled_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetDispatcherTokenAsync(client);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/vehicles/{nonExistentId}/force-release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-1 (integration): 200 on successful force-release
    [Fact]
    public async Task GivenAClaimedVehicle_WhenForceReleaseEndpointCalledByDispatcher_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await ClaimVehicleAsRepAsync(client, "rep1@dealer.com", SeedConstants.Vehicle1Id);
        await SetDispatcherTokenAsync(client);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/force-release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-3 (integration): 403 for ServiceRep role
    [Fact]
    public async Task GivenAServiceRepToken_WhenForceReleaseEndpointCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/force-release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // AC-3 (integration): 401 for unauthenticated
    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenForceReleaseEndpointCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/force-release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
