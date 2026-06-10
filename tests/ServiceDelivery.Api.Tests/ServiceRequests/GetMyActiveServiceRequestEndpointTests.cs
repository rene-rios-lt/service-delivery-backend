using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.ServiceRequests.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.ServiceRequests;

public class GetMyActiveServiceRequestEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private static async Task SeedServiceRequestAsync(
        CustomWebApplicationFactory factory,
        Guid requestId,
        Guid dealerId,
        Guid requesterId,
        Guid dtcId,
        ServiceTier tier,
        ServiceRequestStatus status,
        Guid? assignedRepId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = dealerId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = tier,
            Status = status,
            AssignedRepId = assignedRepId,
            CreatedAt = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GivenARepWithNoActiveRequest_WhenGetMyActiveCalled_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/service-requests/my-active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenARepWithAnAssignedRequest_WhenGetMyActiveCalled_ThenReturnsActiveRequestWithCorrectFields()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await SeedServiceRequestAsync(factory, requestId, SeedConstants.DealerId,
            SeedConstants.Gold1Id, SeedConstants.Dtc001Id, ServiceTier.Gold,
            ServiceRequestStatus.Assigned, SeedConstants.Rep1Id);

        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/service-requests/my-active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MyActiveServiceRequestDto>();
        dto.Should().NotBeNull();
        dto!.RequestId.Should().Be(requestId);
        dto.Tier.Should().Be("Gold");
        dto.DtcTitle.Should().Be("Hydraulic system fault");
        dto.Status.Should().Be("Assigned");
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenGetMyActiveCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/service-requests/my-active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetMyActiveCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/service-requests/my-active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenARequesterToken_WhenGetMyActiveCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "bronze1@example.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/service-requests/my-active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
