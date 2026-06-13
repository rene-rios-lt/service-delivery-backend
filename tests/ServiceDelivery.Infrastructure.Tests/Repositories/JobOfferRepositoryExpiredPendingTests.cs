using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class JobOfferRepositoryExpiredPendingTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"JobOfferRepositoryExpiredPendingTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenAPendingAndExpiredOffer_WhenGetExpiredPendingQueried_ThenOnlyPendingPastExpiryReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var duePendingId = Guid.NewGuid();

        context.JobOffers.AddRange(
            // Pending and past expiry -> should be returned
            new JobOffer { Id = duePendingId, ServiceRequestId = Guid.NewGuid(), RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf.AddSeconds(-1), Status = JobOfferStatus.Pending },
            // Pending but not yet expired -> excluded
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = Guid.NewGuid(), OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Pending },
            // Already Accepted/Declined/Expired past expiry -> excluded
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-120), ExpiresAt = asOf.AddSeconds(-60), Status = JobOfferStatus.Accepted },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-120), ExpiresAt = asOf.AddSeconds(-60), Status = JobOfferStatus.Declined },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-120), ExpiresAt = asOf.AddSeconds(-60), Status = JobOfferStatus.Expired });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetExpiredPendingAsync(asOf);

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be(duePendingId);
        result[0].Status.Should().Be(JobOfferStatus.Pending);
    }

    [Fact]
    public async Task GivenAnOfferExpiringExactlyAtAsOf_WhenGetExpiredPendingQueried_ThenItIsReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var boundaryId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = boundaryId, ServiceRequestId = Guid.NewGuid(), RepId = Guid.NewGuid(), OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf, Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetExpiredPendingAsync(asOf);

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be(boundaryId);
    }

    [Fact]
    public async Task GivenNoExpiredPendingOffers_WhenGetExpiredPendingQueried_ThenReturnsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = Guid.NewGuid(), OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetExpiredPendingAsync(asOf);

        // Assert
        result.Should().BeEmpty();
    }
}
