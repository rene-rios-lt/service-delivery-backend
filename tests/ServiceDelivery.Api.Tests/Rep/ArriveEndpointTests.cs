using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.Rep.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Rep;

public class ArriveEndpointTests
{
    private static async Task<Guid> SeedArrivalScenarioAsync(
        CustomWebApplicationFactory factory,
        Guid repId,
        ServiceRequestStatus requestStatus = ServiceRequestStatus.Assigned,
        RepState repState = RepState.Within15Miles)
    {
        var requestId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Bronze1Id,
            DtcId = SeedConstants.Dtc001Id,
            Tier = ServiceTier.Gold,
            Status = requestStatus,
            AssignedRepId = repId,
            Latitude = 41.6,
            Longitude = -93.6,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = repState,
            ActiveRequestId = requestId,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();
        return requestId;
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
    public async Task GivenAnAssignedRequestAndWithin15MilesRep_WhenArrivePosted_ThenReturns200WithInProgressResult()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = await SeedArrivalScenarioAsync(factory, SeedConstants.Rep1Id);
        var client = await AuthedClientAsync(factory, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync("/rep/arrive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ArriveResult>();
        result.Should().NotBeNull();
        result!.RepId.Should().Be(SeedConstants.Rep1Id);
        result.RequestId.Should().Be(requestId);
        result.RepState.Should().Be("OnSite");
        result.RequestStatus.Should().Be("InProgress");
    }

    [Fact]
    public async Task GivenARepNotWithin15Miles_WhenArrivePosted_ThenReturns400()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        await SeedArrivalScenarioAsync(factory, SeedConstants.Rep1Id, repState: RepState.EnRoute);
        var client = await AuthedClientAsync(factory, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync("/rep/arrive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GivenARepWithNoActiveRequest_WhenArrivePosted_ThenReturns400()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "rep1@dealer.com");

        // Act
        var response = await client.PostAsync("/rep/arrive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenArrivePosted_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.PostAsync("/rep/arrive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenARequesterToken_WhenArrivePosted_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "bronze1@example.com");

        // Act
        var response = await client.PostAsync("/rep/arrive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenArrivePosted_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/rep/arrive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
