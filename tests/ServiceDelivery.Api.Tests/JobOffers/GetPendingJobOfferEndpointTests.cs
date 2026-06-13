using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.JobOffers.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.JobOffers;

public class GetPendingJobOfferEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private static async Task SeedPendingOfferAsync(
        CustomWebApplicationFactory factory,
        Guid offerId,
        Guid requestId,
        Guid repId,
        Guid requesterId,
        Guid dtcId,
        ServiceTier tier,
        DateTime expiresAt,
        double requesterLat,
        double requesterLng,
        Guid? claimVehicleId = null,
        double? vehicleLat = null,
        double? vehicleLng = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = tier,
            Status = ServiceRequestStatus.Pending,
            Latitude = requesterLat,
            Longitude = requesterLng,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.JobOffers.Add(new JobOffer
        {
            Id = offerId,
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc),
            ExpiresAt = expiresAt,
            Status = JobOfferStatus.Pending
        });

        if (claimVehicleId is not null)
        {
            var vehicle = await db.Vehicles.FindAsync(claimVehicleId.Value);
            if (vehicle is not null)
            {
                vehicle.ClaimedByRepId = repId;
                vehicle.LastLatitude = vehicleLat;
                vehicle.LastLongitude = vehicleLng;
            }
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GivenARepWithAPendingOffer_WhenGetPendingCalled_ThenReturns200WithThatOffer()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        await SeedPendingOfferAsync(factory, offerId, Guid.NewGuid(), SeedConstants.Rep1Id,
            SeedConstants.Gold1Id, SeedConstants.Dtc001Id, ServiceTier.Gold,
            new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc), 41.6, -93.6);

        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/job-offers/pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PendingJobOfferDto>();
        dto.Should().NotBeNull();
        dto!.OfferId.Should().Be(offerId);
    }

    [Fact]
    public async Task GivenARepWithAPendingOffer_WhenGetPendingCalled_ThenResponseBodyContainsAllRequiredFields()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        var expiresAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);
        await SeedPendingOfferAsync(factory, offerId, Guid.NewGuid(), SeedConstants.Rep1Id,
            SeedConstants.Gold1Id, SeedConstants.Dtc001Id, ServiceTier.Gold,
            expiresAt, 41.6, -93.6,
            SeedConstants.Vehicle1Id, 41.5, -93.6);

        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/job-offers/pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PendingJobOfferDto>();
        dto.Should().NotBeNull();
        dto!.OfferId.Should().Be(offerId);
        dto.RequesterName.Should().Be("Gold User 1");
        dto.Tier.Should().Be("Gold");
        dto.DtcTitle.Should().Be("Hydraulic system fault");
        dto.RequesterLocation.Should().NotBeNull();
        dto.RequesterLocation.Lat.Should().Be(41.6);
        dto.RequesterLocation.Lng.Should().Be(-93.6);
        dto.DistanceMiles.Should().NotBeNull();
        dto.EtaMinutes.Should().NotBeNull();
        dto.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GivenARepWithNoPendingOffer_WhenGetPendingCalled_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/job-offers/pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenGetPendingCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/job-offers/pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenARequesterToken_WhenGetPendingCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "bronze1@example.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/job-offers/pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetPendingCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/job-offers/pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
