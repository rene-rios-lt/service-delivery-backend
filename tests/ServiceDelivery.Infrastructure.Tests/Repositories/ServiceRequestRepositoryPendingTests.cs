using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class ServiceRequestRepositoryPendingTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid OtherDealerId = Guid.NewGuid();

    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ServiceRequestPendingTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static ServiceRequest Request(Guid dealerId, ServiceRequestStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            RequesterId = Guid.NewGuid(),
            DtcId = Guid.NewGuid(),
            Latitude = 1.0,
            Longitude = 2.0,
            Status = status,
            Tier = ServiceTier.Bronze,
            CreatedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task GivenMixedStatusRequests_WhenGetPendingByDealer_ThenOnlyPendingSameDealerReturned()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var pending1 = Request(DealerId, ServiceRequestStatus.Pending);
        var pending2 = Request(DealerId, ServiceRequestStatus.Pending);
        var assigned = Request(DealerId, ServiceRequestStatus.Assigned);
        var completed = Request(DealerId, ServiceRequestStatus.Completed);
        var otherDealerPending = Request(OtherDealerId, ServiceRequestStatus.Pending);
        context.ServiceRequests.AddRange(pending1, pending2, assigned, completed, otherDealerPending);
        await context.SaveChangesAsync();
        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetPendingByDealerAsync(DealerId);

        // Assert
        result.Select(r => r.Id).Should().BeEquivalentTo(new[] { pending1.Id, pending2.Id });
    }
}
