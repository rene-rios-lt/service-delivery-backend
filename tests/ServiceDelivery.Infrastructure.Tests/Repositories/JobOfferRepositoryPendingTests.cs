using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class JobOfferRepositoryPendingTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"JobOfferRepositoryPendingTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenMixedOffersForReps_WhenGetPendingByRepId_ThenOnlyThatRepsPendingOfferReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repId = Guid.NewGuid();
        var otherRepId = Guid.NewGuid();
        var pendingOfferId = Guid.NewGuid();

        context.JobOffers.AddRange(
            new JobOffer { Id = pendingOfferId, ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddSeconds(60), Status = JobOfferStatus.Pending },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = DateTime.UtcNow.AddMinutes(-5), ExpiresAt = DateTime.UtcNow, Status = JobOfferStatus.Declined },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = DateTime.UtcNow.AddMinutes(-10), ExpiresAt = DateTime.UtcNow, Status = JobOfferStatus.Expired },
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = otherRepId, OfferedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddSeconds(60), Status = JobOfferStatus.Pending });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetPendingByRepIdAsync(repId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(pendingOfferId);
        result.RepId.Should().Be(repId);
        result.Status.Should().Be(JobOfferStatus.Pending);
    }

    [Fact]
    public async Task GivenNoPendingOfferForRep_WhenGetPendingByRepId_ThenReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repId = Guid.NewGuid();

        context.JobOffers.Add(
            new JobOffer { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), RepId = repId, OfferedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow, Status = JobOfferStatus.Declined });
        await context.SaveChangesAsync();

        var repository = new JobOfferRepository(context);

        // Act
        var result = await repository.GetPendingByRepIdAsync(repId);

        // Assert
        result.Should().BeNull();
    }
}
