using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class JobOfferRepositoryTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"JobOfferRepositoryTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenAJobOffer_WhenAddAsyncCalled_ThenItIsPersisted()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new JobOfferRepository(context);
        var offerId = Guid.NewGuid();
        var offer = new JobOffer
        {
            Id = offerId,
            ServiceRequestId = Guid.NewGuid(),
            RepId = Guid.NewGuid(),
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = JobOfferStatus.Pending
        };

        // Act
        await repository.AddAsync(offer);

        // Assert
        var persisted = await context.JobOffers.FirstOrDefaultAsync(o => o.Id == offerId);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(JobOfferStatus.Pending);
    }

    [Fact]
    public async Task GivenDeclinedAndExpiredOffers_WhenGetSkippedRepIds_ThenBothReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var requestId = Guid.NewGuid();
        var declinedRep = Guid.NewGuid();
        var expiredRep = Guid.NewGuid();
        var pendingRep = Guid.NewGuid();
        var otherRequestRep = Guid.NewGuid();

        context.JobOffers.AddRange(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = requestId, RepId = declinedRep, OfferedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow, Status = JobOfferStatus.Declined },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = requestId, RepId = expiredRep, OfferedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow, Status = JobOfferStatus.Expired },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = requestId, RepId = pendingRep, OfferedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow, Status = JobOfferStatus.Pending },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = otherRequestRep, OfferedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow, Status = JobOfferStatus.Declined });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var skipped = await repository.GetSkippedRepIdsForRequestAsync(requestId);

        // Assert
        skipped.Should().BeEquivalentTo(new[] { declinedRep, expiredRep });
        skipped.Should().NotContain(pendingRep);
        skipped.Should().NotContain(otherRequestRep);
    }
}
