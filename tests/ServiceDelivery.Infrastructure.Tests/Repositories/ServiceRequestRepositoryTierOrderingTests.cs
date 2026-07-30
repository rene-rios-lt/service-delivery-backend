using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class ServiceRequestRepositoryTierOrderingTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ServiceRequestTierOrderingTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static ServiceRequest Request(ServiceTier tier, DateTime createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            DealerId = DealerId,
            RequesterId = Guid.NewGuid(),
            DtcId = Guid.NewGuid(),
            Latitude = 1.0,
            Longitude = 2.0,
            Status = ServiceRequestStatus.Pending,
            Tier = tier,
            CreatedAt = createdAt
        };

    [Fact]
    public async Task GivenSilverInsertedBeforeGold_WhenGetPendingByDealer_ThenGoldReturnedFirst()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var silver = Request(ServiceTier.Silver, DateTime.UtcNow.AddMinutes(-2));
        var gold = Request(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-1));
        context.ServiceRequests.AddRange(silver, gold);
        await context.SaveChangesAsync();
        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetPendingByDealerAsync(DealerId);

        // Assert
        result[0].Tier.Should().Be(ServiceTier.Gold);
        result[1].Tier.Should().Be(ServiceTier.Silver);
    }

    [Fact]
    public async Task GivenSameTierRequestsWithDifferentCreatedAt_WhenGetPendingByDealer_ThenOlderCreatedAtIsFirst()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var goldOld = Request(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-5));
        var goldNew = Request(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-1));
        context.ServiceRequests.AddRange(goldNew, goldOld);
        await context.SaveChangesAsync();
        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetPendingByDealerAsync(DealerId);

        // Assert
        result[0].Id.Should().Be(goldOld.Id);
        result[1].Id.Should().Be(goldNew.Id);
    }

    [Fact]
    public async Task GivenGoldSilverBronzeRequestsInsertedInReverseOrder_WhenGetPendingByDealer_ThenTierDescendingOrderPreserved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var bronze = Request(ServiceTier.Bronze, DateTime.UtcNow.AddMinutes(-3));
        var silver = Request(ServiceTier.Silver, DateTime.UtcNow.AddMinutes(-2));
        var gold = Request(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-1));
        context.ServiceRequests.AddRange(bronze, silver, gold);
        await context.SaveChangesAsync();
        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetPendingByDealerAsync(DealerId);

        // Assert
        result[0].Tier.Should().Be(ServiceTier.Gold);
        result[1].Tier.Should().Be(ServiceTier.Silver);
        result[2].Tier.Should().Be(ServiceTier.Bronze);
    }
}
