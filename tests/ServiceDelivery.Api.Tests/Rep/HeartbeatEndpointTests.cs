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

namespace ServiceDelivery.Api.Tests.Rep;

public class HeartbeatEndpointTests
{
    private static async Task SeedRepStateAsync(CustomWebApplicationFactory factory, Guid repId, DateTime? lastHeartbeatAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            HumanControlled = true,
            LastHeartbeatAt = lastHeartbeatAt,
            UpdatedAt = new DateTime(2026, 6, 16, 9, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();
    }

    private static async Task<HttpClient> AuthedClientAsync(CustomWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, SeedConstants.DefaultPassword));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        return client;
    }

    [Fact]
    public async Task GivenAServiceRepToken_WhenPostingHeartbeat_ThenLastHeartbeatAtIsPersisted()
    {
        // Arrange — rep1 already has a state row with a stale heartbeat (exercises the UpsertAsync existing-row path)
        await using var factory = new CustomWebApplicationFactory();
        var staleHeartbeat = new DateTime(2026, 6, 16, 8, 0, 0, DateTimeKind.Utc);
        await SeedRepStateAsync(factory, SeedConstants.Rep1Id, staleHeartbeat);
        var client = await AuthedClientAsync(factory, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync("/rep/heartbeat", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.RepStateRecords.AsNoTracking().FirstAsync(r => r.RepId == SeedConstants.Rep1Id);
        state.LastHeartbeatAt.Should().NotBeNull();
        state.LastHeartbeatAt!.Value.Should().BeAfter(staleHeartbeat);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenPostingHeartbeat_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.PostAsync("/rep/heartbeat", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
