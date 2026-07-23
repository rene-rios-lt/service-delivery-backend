using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.JobOffers.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.JobOffers;

public class AcceptJobOfferEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private static async Task<Guid> SeedOfferAsync(
        CustomWebApplicationFactory factory,
        Guid offerId,
        Guid repId,
        JobOfferStatus offerStatus = JobOfferStatus.Pending,
        Guid? claimVehicleId = null,
        double? vehicleLat = null,
        double? vehicleLng = null)
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
            Status = ServiceRequestStatus.Pending,
            Latitude = 41.6,
            Longitude = -93.6,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.JobOffers.Add(new JobOffer
        {
            Id = offerId,
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            Status = offerStatus
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
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
        return requestId;
    }

    private static async Task<(Guid OfferId, Guid ExistingRequestId)> SeedOfferForRepAlreadyOnActiveJobAsync(
        CustomWebApplicationFactory factory,
        Guid repId)
    {
        var existingRequestId = Guid.NewGuid();
        var newRequestId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = existingRequestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Bronze1Id,
            DtcId = SeedConstants.Dtc001Id,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId,
            Latitude = 41.6,
            Longitude = -93.6,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = newRequestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Bronze2Id,
            DtcId = SeedConstants.Dtc001Id,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Pending,
            Latitude = 41.7,
            Longitude = -93.7,
            CreatedAt = new DateTime(2026, 6, 13, 9, 5, 0, DateTimeKind.Utc)
        });

        db.JobOffers.Add(new JobOffer
        {
            Id = offerId,
            ServiceRequestId = newRequestId,
            RepId = repId,
            OfferedAt = new DateTime(2026, 6, 13, 9, 5, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            Status = JobOfferStatus.Pending
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.EnRoute,
            ActiveRequestId = existingRequestId,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();
        return (offerId, existingRequestId);
    }

    private static async Task<Guid?> GetActiveRequestIdAsync(CustomWebApplicationFactory factory, Guid repId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.RepStateRecords.FindAsync(repId);
        return record?.ActiveRequestId;
    }

    [Fact]
    public async Task GivenARepAlreadyOnActiveJob_WhenAcceptPosted_ThenReturns409Conflict()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (offerId, existingRequestId) = await SeedOfferForRepAlreadyOnActiveJobAsync(factory, SeedConstants.Rep1Id);

        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/job-offers/{offerId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var activeRequestId = await GetActiveRequestIdAsync(factory, SeedConstants.Rep1Id);
        activeRequestId.Should().Be(existingRequestId);
    }

    [Fact]
    public async Task GivenARepWithAPendingOffer_WhenPostedToAccept_ThenReturns200WithAcceptedResult()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        var requestId = await SeedOfferAsync(factory, offerId, SeedConstants.Rep1Id,
            claimVehicleId: SeedConstants.Vehicle1Id, vehicleLat: 41.5, vehicleLng: -93.6);

        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/job-offers/{offerId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AcceptJobOfferResult>();
        result.Should().NotBeNull();
        result!.OfferId.Should().Be(offerId);
        result.RequestId.Should().Be(requestId);
        result.OfferStatus.Should().Be("Accepted");
        result.RequestStatus.Should().Be("Assigned");
        result.RepState.Should().Be("EnRoute");
    }

    [Theory]
    [InlineData(JobOfferStatus.Expired)]
    [InlineData(JobOfferStatus.Declined)]
    [InlineData(JobOfferStatus.Accepted)]
    public async Task GivenANonPendingOffer_WhenPostedToAccept_ThenReturns409(JobOfferStatus status)
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        await SeedOfferAsync(factory, offerId, SeedConstants.Rep1Id, offerStatus: status);

        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/job-offers/{offerId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task<(Guid OfferAId, Guid RequestAId, Guid OfferBId)> SeedTwoOffersForSameRepAsync(
        CustomWebApplicationFactory factory,
        Guid repId)
    {
        var requestAId = Guid.NewGuid();
        var requestBId = Guid.NewGuid();
        var offerAId = Guid.NewGuid();
        var offerBId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestAId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Bronze1Id,
            DtcId = SeedConstants.Dtc001Id,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Pending,
            Latitude = 41.6,
            Longitude = -93.6,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestBId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Bronze2Id,
            DtcId = SeedConstants.Dtc001Id,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Pending,
            Latitude = 41.7,
            Longitude = -93.7,
            CreatedAt = new DateTime(2026, 6, 13, 9, 5, 0, DateTimeKind.Utc)
        });

        db.JobOffers.Add(new JobOffer
        {
            Id = offerAId,
            ServiceRequestId = requestAId,
            RepId = repId,
            OfferedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            Status = JobOfferStatus.Pending
        });

        db.JobOffers.Add(new JobOffer
        {
            Id = offerBId,
            ServiceRequestId = requestBId,
            RepId = repId,
            OfferedAt = new DateTime(2026, 6, 13, 9, 5, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            Status = JobOfferStatus.Pending
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();
        return (offerAId, requestAId, offerBId);
    }

    // AC-3 (Option B): sequenced-race safety. The first accept commits (rep -> EnRoute with
    // ActiveRequestId = Request A); a second accept for the same rep then reads the committed state,
    // the IsOnActiveJob() guard fires, and the endpoint returns 409 without clobbering the first
    // assignment. This verifies SEQUENTIAL protection only. A truly simultaneous double-read race
    // (both requests read Available before either commits) is not prevented under InMemory EF and
    // would require an optimistic-concurrency token (Option A). Documented as a POC limitation.
    [Fact]
    public async Task GivenARepAlreadyEnRouteFromAPriorCommittedAccept_WhenSecondAcceptPosted_ThenReturns409AndRepRetainsOriginalActiveRequestId()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var (offerAId, requestAId, offerBId) = await SeedTwoOffersForSameRepAsync(factory, SeedConstants.Rep1Id);

        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var firstResponse = await client.PostAsync($"/job-offers/{offerAId}/accept", null);
        var secondResponse = await client.PostAsync($"/job-offers/{offerBId}/accept", null);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var activeRequestId = await GetActiveRequestIdAsync(factory, SeedConstants.Rep1Id);
        activeRequestId.Should().Be(requestAId);
    }

    [Fact]
    public async Task GivenANonExistentOfferId_WhenPostedToAccept_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/job-offers/{Guid.NewGuid()}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenPostedToAccept_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/job-offers/{Guid.NewGuid()}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenPostedToAccept_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/job-offers/{Guid.NewGuid()}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
