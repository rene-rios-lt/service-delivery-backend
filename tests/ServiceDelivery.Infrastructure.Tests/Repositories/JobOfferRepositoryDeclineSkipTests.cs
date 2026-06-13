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
}
