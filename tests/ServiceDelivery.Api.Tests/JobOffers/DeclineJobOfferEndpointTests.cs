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

public class DeclineJobOfferEndpointTests
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
        JobOfferStatus offerStatus = JobOfferStatus.Pending)
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

        await db.SaveChangesAsync();
        return requestId;
    }

    [Fact]
    public async Task GivenAPendingOffer_WhenDeclineEndpointCalled_ThenReturns200WithDeclinedStatus()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        var requestId = await SeedOfferAsync(factory, offerId, SeedConstants.Rep1Id);

        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/job-offers/{offerId}/decline", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DeclineJobOfferResult>();
        result.Should().NotBeNull();
        result!.OfferId.Should().Be(offerId);
        result.RequestId.Should().Be(requestId);
        result.OfferStatus.Should().Be("Declined");
    }

    [Theory]
    [InlineData(JobOfferStatus.Accepted)]
    [InlineData(JobOfferStatus.Expired)]
    public async Task GivenANonPendingOffer_WhenDeclineEndpointCalled_ThenReturns409(JobOfferStatus status)
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
        var response = await client.PostAsync($"/job-offers/{offerId}/decline", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GivenANonExistentOfferId_WhenDeclineEndpointCalled_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/job-offers/{Guid.NewGuid()}/decline", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenDeclineEndpointCalled_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync($"/job-offers/{Guid.NewGuid()}/decline", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
