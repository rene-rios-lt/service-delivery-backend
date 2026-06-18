using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class ServiceRequestRepositoryTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ServiceRequestRepositoryTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static ServiceRequest Request(Guid id, ServiceRequestStatus status)
        => new()
        {
            Id = id,
            DealerId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            DtcId = Guid.NewGuid(),
            Latitude = 0,
            Longitude = 0,
            Status = status,
            Tier = ServiceTier.Bronze,
            CreatedAt = DateTime.UtcNow
        };

    private static JobOffer Offer(Guid requestId, JobOfferStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            RepId = Guid.NewGuid(),
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = status
        };

    [Fact]
    public async Task GivenPendingRequestsSomeWithNoPendingOffer_WhenGetOrphanedPendingCalled_ThenOnlyOrphansReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var orphan1 = Request(Guid.NewGuid(), ServiceRequestStatus.Pending);
        var orphan2 = Request(Guid.NewGuid(), ServiceRequestStatus.Pending);
        var coveredByPendingOffer = Request(Guid.NewGuid(), ServiceRequestStatus.Pending);
        var assigned = Request(Guid.NewGuid(), ServiceRequestStatus.Assigned);

        context.ServiceRequests.AddRange(orphan1, orphan2, coveredByPendingOffer, assigned);
        context.JobOffers.Add(Offer(coveredByPendingOffer.Id, JobOfferStatus.Pending));
        await context.SaveChangesAsync();

        var repository = new ServiceRequestRepository(context);

        // Act
        var orphans = await repository.GetOrphanedPendingAsync();

        // Assert
        orphans.Select(r => r.Id).Should().BeEquivalentTo(new[] { orphan1.Id, orphan2.Id });
    }

    [Fact]
    public async Task GivenAPendingRequestWithAPendingOffer_WhenGetOrphanedPendingCalled_ThenItIsExcluded()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var covered = Request(Guid.NewGuid(), ServiceRequestStatus.Pending);
        context.ServiceRequests.Add(covered);
        context.JobOffers.Add(Offer(covered.Id, JobOfferStatus.Pending));
        await context.SaveChangesAsync();

        var repository = new ServiceRequestRepository(context);

        // Act
        var orphans = await repository.GetOrphanedPendingAsync();

        // Assert
        orphans.Should().NotContain(r => r.Id == covered.Id);
    }

    [Fact]
    public async Task GivenAPendingRequestWhoseOnlyOffersAreNonPending_WhenGetOrphanedPendingCalled_ThenItIsReturned()
    {
        // Arrange: a request whose offers all declined/expired is still orphaned and must be re-matched.
        using var context = CreateInMemoryContext();
        var orphan = Request(Guid.NewGuid(), ServiceRequestStatus.Pending);
        context.ServiceRequests.Add(orphan);
        context.JobOffers.Add(Offer(orphan.Id, JobOfferStatus.Declined));
        context.JobOffers.Add(Offer(orphan.Id, JobOfferStatus.Expired));
        await context.SaveChangesAsync();

        var repository = new ServiceRequestRepository(context);

        // Act
        var orphans = await repository.GetOrphanedPendingAsync();

        // Assert
        orphans.Select(r => r.Id).Should().Contain(orphan.Id);
    }
}
