using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class JobOfferRepositoryLivePendingTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"JobOfferRepositoryLivePendingTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenAPendingOfferWithFutureExpiry_WhenGetLivePendingOfferForRequest_ThenOfferIsReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var requestId = Guid.NewGuid();
        var liveOfferId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = liveOfferId, ServiceRequestId = requestId, RepId = Guid.NewGuid(), OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetLivePendingOfferForRequestAsync(requestId, asOf);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(liveOfferId);
        result.Status.Should().Be(JobOfferStatus.Pending);
    }

    [Fact]
    public async Task GivenAPendingOfferPastExpiry_WhenGetLivePendingOfferForRequest_ThenNullIsReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var requestId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = requestId, RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf.AddSeconds(-1), Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetLivePendingOfferForRequestAsync(requestId, asOf);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenAPendingOfferExpiringExactlyAtAsOf_WhenGetLivePendingOfferForRequest_ThenNullIsReturned()
    {
        // Arrange
        // Boundary: the predicate is strictly ExpiresAt > asOf, so an offer expiring exactly at asOf is dead.
        // This is the exact complement of GetExpiredPendingAsync (ExpiresAt <= asOf returns it as expired).
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var requestId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = requestId, RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf, Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetLivePendingOfferForRequestAsync(requestId, asOf);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenAnExpiredStatusOffer_WhenGetLivePendingOfferForRequest_ThenNullIsReturned()
    {
        // Arrange
        // BUG-058/BUG-054: an Expired-status offer (even with a future ExpiresAt) must never count as live,
        // so it can never block a re-offer.
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var requestId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = requestId, RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Expired });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetLivePendingOfferForRequestAsync(requestId, asOf);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenADeclinedOffer_WhenGetLivePendingOfferForRequest_ThenNullIsReturned()
    {
        // Arrange
        // BUG-054 preservation: a Declined offer (even unexpired) must not count as live.
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var requestId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = requestId, RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Declined });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetLivePendingOfferForRequestAsync(requestId, asOf);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenNoOffersForRequest_WhenGetLivePendingOfferForRequest_ThenNullIsReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var requestId = Guid.NewGuid();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetLivePendingOfferForRequestAsync(requestId, asOf);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenALivePendingOfferForDifferentRequest_WhenGetLivePendingOfferForRequest_ThenNullIsReturned()
    {
        // Arrange
        // A live offer belonging to another request must not match the queried request id.
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var queriedRequestId = Guid.NewGuid();
        var otherRequestId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = otherRequestId, RepId = Guid.NewGuid(), OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetLivePendingOfferForRequestAsync(queriedRequestId, asOf);

        // Assert
        result.Should().BeNull();
    }
}
