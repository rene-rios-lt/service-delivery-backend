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

public class ReleaseVehicleEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private async Task ClaimVehicleAsync(HttpClient client, string email, Guid vehicleId)
    {
        var token = await GetTokenAsync(client, email, SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await client.PostAsync($"/vehicles/{vehicleId}/claim", null);
    }

    // AC-1 (integration): happy path — 200 on successful release
    [Fact]
    public async Task GivenAClaimedVehicle_WhenReleaseEndpointCalled_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await ClaimVehicleAsync(client, "rep1@dealer.com", SeedConstants.Vehicle1Id);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-3 (integration): 400 when vehicle is unclaimed
    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenReleaseEndpointCalled_ThenReturns400()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act — Vehicle1 is unclaimed (no prior claim call)
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-3 (integration): 400 when vehicle is claimed by a different rep
    [Fact]
    public async Task GivenAVehicleClaimedByAnotherRep_WhenReleaseEndpointCalled_ThenReturns400()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Rep1 claims Vehicle1
        await ClaimVehicleAsync(client, "rep1@dealer.com", SeedConstants.Vehicle1Id);

        // Rep2 tries to release Vehicle1 (claimed by Rep1)
        var rep2Token = await GetTokenAsync(client, "rep2@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rep2Token);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-4 (integration): 400 when rep has active InProgress job (OnSite state)
    [Fact]
    public async Task GivenARepWithActiveInProgressJob_WhenReleaseEndpointCalled_ThenReturns400()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Rep1 claims Vehicle1
        await ClaimVehicleAsync(client, "rep1@dealer.com", SeedConstants.Vehicle1Id);

        // Manually set rep state to OnSite (simulating an active InProgress job)
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var repState = await db.RepStateRecords.FirstOrDefaultAsync(r => r.RepId == SeedConstants.Rep1Id);
            if (repState is not null)
            {
                repState.State = RepState.OnSite;
                repState.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.RepStateRecords.Add(new RepStateRecord
                {
                    RepId = SeedConstants.Rep1Id,
                    State = RepState.OnSite,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Auth: 401 if unauthenticated
    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenReleaseEndpointCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Auth: 403 if non-ServiceRep role
    [Fact]
    public async Task GivenADispatcherToken_WhenReleaseEndpointCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/vehicles/{SeedConstants.Vehicle1Id}/release", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
