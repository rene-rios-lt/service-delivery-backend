using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class JobOfferRepositoryBusyRepTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"JobOfferRepositoryBusyRepTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenARepWithALivePendingOffer_WhenGetRepIdsWithLivePendingOffer_ThenRepIdIsReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var repId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetRepIdsWithLivePendingOfferAsync(asOf);

        // Assert
        result.Should().Contain(repId);
    }

    [Fact]
    public async Task GivenARepWithAnExpiredPendingOffer_WhenGetRepIdsWithLivePendingOffer_ThenRepIdIsNotReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var repId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf.AddSeconds(-1), Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetRepIdsWithLivePendingOfferAsync(asOf);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenARepWithPendingOfferExpiringExactlyAtAsOf_WhenGetRepIdsWithLivePendingOffer_ThenRepIdIsNotReturned()
    {
        // Arrange
        // Boundary: the predicate is strictly ExpiresAt > asOf, so an offer expiring exactly at asOf is dead.
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var repId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf, Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetRepIdsWithLivePendingOfferAsync(asOf);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenARepWithADeclinedOffer_WhenGetRepIdsWithLivePendingOffer_ThenRepIdIsNotReturned()
    {
        // Arrange
        // A Declined offer (even unexpired) must never mark a rep busy.
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var repId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Declined });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetRepIdsWithLivePendingOfferAsync(asOf);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenARepWithAnExpiredStatusOffer_WhenGetRepIdsWithLivePendingOffer_ThenRepIdIsNotReturned()
    {
        // Arrange
        // BUG-054 guarantee: a swept Expired-status offer (even with a future ExpiresAt) must never
        // mark a rep busy, so it can never block a re-offer.
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var repId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = asOf.AddSeconds(-60), ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Expired });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetRepIdsWithLivePendingOfferAsync(asOf);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenARepWithAnAcceptedOffer_WhenGetRepIdsWithLivePendingOffer_ThenRepIdIsNotReturned()
    {
        // Arrange
        // An Accepted offer is no longer a soft reservation — the rep's availability is governed by rep state.
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var repId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Accepted });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetRepIdsWithLivePendingOfferAsync(asOf);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenNoJobOffers_WhenGetRepIdsWithLivePendingOffer_ThenEmptyListIsReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetRepIdsWithLivePendingOfferAsync(asOf);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenMultipleRepsWithLivePendingOffers_WhenGetRepIdsWithLivePendingOffer_ThenAllAreReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var asOf = new DateTime(2026, 06, 13, 12, 00, 00, DateTimeKind.Utc);
        var rep1 = Guid.NewGuid();
        var rep2 = Guid.NewGuid();
        var rep3 = Guid.NewGuid();

        context.JobOffers.AddRange(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = rep1, OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Pending },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = rep2, OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Pending },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = rep3, OfferedAt = asOf, ExpiresAt = asOf.AddSeconds(60), Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetRepIdsWithLivePendingOfferAsync(asOf);

        // Assert
        result.Should().BeEquivalentTo(new[] { rep1, rep2, rep3 });
    }
}
