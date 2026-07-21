using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class JobOfferRepositoryDeclineSkipTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"JobOfferRepositoryDeclineSkipTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenADeclinedOfferPersisted_WhenGetSkippedRepIdsForRequest_ThenDecliningRepIsIncluded()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var requestId = Guid.NewGuid();
        var decliningRepId = Guid.NewGuid();

        var offer = new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            RepId = decliningRepId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = JobOfferStatus.Pending
        };
        context.JobOffers.Add(offer);
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        offer.Decline();
        await repository.UpdateAsync(offer);
        var skipped = await repository.GetSkippedRepIdsForRequestAsync(requestId);

        // Assert
        skipped.Should().Contain(decliningRepId);
    }

    [Fact]
    public async Task GivenAnExpiredOfferPersisted_WhenGetSkippedRepIdsForRequest_ThenExpiredRepIsNotIncluded()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var requestId = Guid.NewGuid();
        var expiredRepId = Guid.NewGuid();

        var offer = new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            RepId = expiredRepId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = JobOfferStatus.Pending
        };
        context.JobOffers.Add(offer);
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        offer.Expire();
        await repository.UpdateAsync(offer);
        var skipped = await repository.GetSkippedRepIdsForRequestAsync(requestId);

        // Assert
        skipped.Should().NotContain(expiredRepId);
    }

    [Fact]
    public async Task GivenARepHasBothDeclinedAndExpiredOffersForRequest_WhenGetSkippedRepIdsForRequest_ThenRepIsStillIncluded()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var requestId = Guid.NewGuid();
        var repId = Guid.NewGuid();

        var expiredOffer = new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-4),
            Status = JobOfferStatus.Pending
        };
        var declinedOffer = new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = JobOfferStatus.Pending
        };
        context.JobOffers.AddRange(expiredOffer, declinedOffer);
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        expiredOffer.Expire();
        await repository.UpdateAsync(expiredOffer);
        declinedOffer.Decline();
        await repository.UpdateAsync(declinedOffer);
        var skipped = await repository.GetSkippedRepIdsForRequestAsync(requestId);

        // Assert
        skipped.Should().Contain(repId);
    }
}
