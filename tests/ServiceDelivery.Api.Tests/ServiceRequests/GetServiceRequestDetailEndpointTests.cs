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

public class GetServiceRequestDetailEndpointTests
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
        Guid? assignedRepId = null,
        Action<AppDbContext>? seedOffers = null)
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
            Latitude = 12.5,
            Longitude = -34.25,
            CreatedAt = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc)
        });
        seedOffers?.Invoke(db);
        await db.SaveChangesAsync();
    }

    private static async Task SetAuthAsync(HttpClient client, GetServiceRequestDetailEndpointTests test, string email)
    {
        var token = await test.GetTokenAsync(client, email, SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GivenADispatcherAndAnInDealerRequest_WhenGetByIdCalled_ThenReturns200WithFullDetailShape()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var offeredAt = new DateTime(2026, 6, 10, 9, 5, 0, DateTimeKind.Utc);
        var expiresAt = new DateTime(2026, 6, 10, 9, 10, 0, DateTimeKind.Utc);
        await SeedServiceRequestAsync(factory, requestId, SeedConstants.DealerId,
            SeedConstants.Gold1Id, SeedConstants.Dtc001Id, ServiceTier.Gold,
            ServiceRequestStatus.Assigned, SeedConstants.Rep1Id,
            db => db.JobOffers.Add(new JobOffer
            {
                Id = offerId,
                ServiceRequestId = requestId,
                RepId = SeedConstants.Rep1Id,
                OfferedAt = offeredAt,
                ExpiresAt = expiresAt,
                Status = JobOfferStatus.Accepted
            }));

        var client = factory.CreateClient();
        await SetAuthAsync(client, this, "alex@dealer.com");

        // Act
        var response = await client.GetAsync($"/service-requests/{requestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ServiceRequestDetailDto>();
        dto.Should().NotBeNull();
        dto!.RequestId.Should().Be(requestId);
        dto.RequesterName.Should().Be("Gold User 1");
        dto.Tier.Should().Be("Gold");
        dto.DtcTitle.Should().Be("Hydraulic system fault");
        dto.RequesterLocation.Lat.Should().Be(12.5);
        dto.RequesterLocation.Lng.Should().Be(-34.25);
        dto.Status.Should().Be("Assigned");
        dto.AssignedRep.Should().NotBeNull();
        dto.AssignedRep!.RepId.Should().Be(SeedConstants.Rep1Id);
        dto.AssignedRep.Name.Should().Be("Rep One");
        dto.OfferHistory.Should().HaveCount(1);
        dto.OfferHistory[0].OfferId.Should().Be(offerId);
        dto.OfferHistory[0].RepName.Should().Be("Rep One");
        dto.OfferHistory[0].Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task GivenANonExistentId_WhenGetByIdCalled_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAuthAsync(client, this, "alex@dealer.com");

        // Act
        var response = await client.GetAsync($"/service-requests/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenADispatcher_WhenGetByIdForOtherDealerRequest_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await SeedServiceRequestAsync(factory, requestId, SeedConstants.Dealer2Id,
            SeedConstants.Gold1Id, SeedConstants.Dtc001Id, ServiceTier.Gold,
            ServiceRequestStatus.Pending);

        var client = factory.CreateClient();
        await SetAuthAsync(client, this, "alex@dealer.com");

        // Act
        var response = await client.GetAsync($"/service-requests/{requestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenARequester_WhenGetByIdForAnotherRequestersRequest_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await SeedServiceRequestAsync(factory, requestId, SeedConstants.DealerId,
            SeedConstants.Silver1Id, SeedConstants.Dtc001Id, ServiceTier.Silver,
            ServiceRequestStatus.Pending);

        var client = factory.CreateClient();
        await SetAuthAsync(client, this, "bronze1@example.com");

        // Act
        var response = await client.GetAsync($"/service-requests/{requestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenARequester_WhenGetByIdForOwnRequest_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await SeedServiceRequestAsync(factory, requestId, SeedConstants.DealerId,
            SeedConstants.Bronze1Id, SeedConstants.Dtc001Id, ServiceTier.Bronze,
            ServiceRequestStatus.Pending);

        var client = factory.CreateClient();
        await SetAuthAsync(client, this, "bronze1@example.com");

        // Act
        var response = await client.GetAsync($"/service-requests/{requestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ServiceRequestDetailDto>();
        dto!.RequestId.Should().Be(requestId);
    }

    [Fact]
    public async Task GivenAServiceRep_WhenGetByIdForRequestNotAssignedToThem_ThenReturns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await SeedServiceRequestAsync(factory, requestId, SeedConstants.DealerId,
            SeedConstants.Gold1Id, SeedConstants.Dtc001Id, ServiceTier.Gold,
            ServiceRequestStatus.Assigned, SeedConstants.Rep2Id);

        var client = factory.CreateClient();
        await SetAuthAsync(client, this, "rep1@dealer.com");

        // Act
        var response = await client.GetAsync($"/service-requests/{requestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenAServiceRep_WhenGetByIdForRequestAssignedToThem_ThenReturns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var requestId = Guid.NewGuid();
        await SeedServiceRequestAsync(factory, requestId, SeedConstants.DealerId,
            SeedConstants.Gold1Id, SeedConstants.Dtc001Id, ServiceTier.Gold,
            ServiceRequestStatus.Assigned, SeedConstants.Rep1Id);

        var client = factory.CreateClient();
        await SetAuthAsync(client, this, "rep1@dealer.com");

        // Act
        var response = await client.GetAsync($"/service-requests/{requestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ServiceRequestDetailDto>();
        dto!.RequestId.Should().Be(requestId);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetByIdCalled_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/service-requests/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
