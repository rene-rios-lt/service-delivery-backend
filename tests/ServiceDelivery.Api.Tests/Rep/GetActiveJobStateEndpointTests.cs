using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.Rep.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Rep;

public class GetActiveJobStateEndpointTests
{
    private const double RequesterLat = 41.6;
    private const double RequesterLng = -93.6;
    private const double RepLat = 41.7;
    private const double RepLng = -93.7;

    private static async Task<Guid> SeedActiveJobAsync(
        CustomWebApplicationFactory factory,
        Guid repId,
        Guid vehicleId,
        RepState repState = RepState.EnRoute)
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
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId,
            Latitude = RequesterLat,
            Longitude = RequesterLng,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = repState,
            ActiveRequestId = requestId,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        var vehicle = await db.Vehicles.FindAsync(vehicleId);
        vehicle!.ClaimedByRepId = repId;
        vehicle.ClaimedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc);
        vehicle.LastLatitude = RepLat;
        vehicle.LastLongitude = RepLng;
        vehicle.LastPositionUpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc);

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
    public async Task GivenARepWithAssignedRequestAndVehiclePosition_WhenGetActiveJobStateEndpointCalled_ThenReturns200WithAllFields()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = await SeedActiveJobAsync(factory, SeedConstants.Rep1Id, SeedConstants.Vehicle1Id);
        var client = await AuthedClientAsync(factory, "rep1@dealer.com");
        var expectedEta = (int)Math.Round(HaversineCalculator.EtaMinutes(
            HaversineCalculator.DistanceMiles(RepLat, RepLng, RequesterLat, RequesterLng)));

        // Act
        var response = await client.GetAsync("/rep/active-job-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ActiveJobStateDto>();
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(requestId);
        result.RequesterName.Should().Be("Bronze User 1");
        result.DtcTitle.Should().Be("Hydraulic system fault");
        result.RequesterLat.Should().Be(RequesterLat);
        result.RequesterLng.Should().Be(RequesterLng);
        result.RepLat.Should().Be(RepLat);
        result.RepLng.Should().Be(RepLng);
        result.EtaMinutes.Should().Be(expectedEta);
        result.RepState.Should().Be("EnRoute");
    }

    [Fact]
    public async Task GivenARepWithNoActiveRequest_WhenGetActiveJobStateCalled_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "rep1@dealer.com");

        // Act
        var response = await client.GetAsync("/rep/active-job-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenGetActiveJobStateCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "alex@dealer.com");

        // Act
        var response = await client.GetAsync("/rep/active-job-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenARequesterToken_WhenGetActiveJobStateCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = await AuthedClientAsync(factory, "bronze1@example.com");

        // Act
        var response = await client.GetAsync("/rep/active-job-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetActiveJobStateCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/rep/active-job-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
