using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.JobOffers;

[Collection("Hub Tests")]
public class ExpireJobOfferSignalRTests
{
    private static async Task SeedExpiredPendingOfferAsync(
        CustomWebApplicationFactory factory, Guid offerId, Guid repId)
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

        // Pending offer whose ExpiresAt is already in the past relative to the sweep's asOf.
        db.JobOffers.Add(new JobOffer
        {
            Id = offerId,
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 6, 13, 9, 1, 0, DateTimeKind.Utc),
            Status = JobOfferStatus.Pending
        });

        await db.SaveChangesAsync();
    }

    private static async Task RunSweepAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var sweeper = scope.ServiceProvider.GetRequiredService<IExpiredJobOfferSweeper>();
        await sweeper.SweepAsync(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GivenAConnectedRep_WhenTheirOfferExpires_ThenRepReceivesJobOfferExpiredEvent()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        await SeedExpiredPendingOfferAsync(factory, offerId, SeedConstants.Rep1Id);

        var repToken = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<JobOfferExpiredPayload>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/rep", repToken);
        connection.On<JobOfferExpiredPayload>("JobOfferExpired", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await RunSweepAsync(factory);

        // Assert
        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.OfferId.Should().Be(offerId);

        await connection.StopAsync();
    }
}
