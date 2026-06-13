using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class ServiceRequestRepositoryUpdateTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ServiceRequestUpdateTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenAnAssignedRequest_WhenUpdateAsyncCalled_ThenStatusAndAssignedRepIdArePersisted()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var requestId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            DealerId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            DtcId = Guid.NewGuid(),
            Latitude = 41.6,
            Longitude = -93.6,
            Status = ServiceRequestStatus.Pending,
            Tier = ServiceTier.Gold,
            CreatedAt = DateTime.UtcNow
        };
        context.ServiceRequests.Add(request);
        await context.SaveChangesAsync();
        var repository = new ServiceRequestRepository(context);

        // Act
        request.Status = ServiceRequestStatus.Assigned;
        request.AssignedRepId = repId;
        await repository.UpdateAsync(request);

        // Assert
        var persisted = await context.ServiceRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        persisted!.Status.Should().Be(ServiceRequestStatus.Assigned);
        persisted.AssignedRepId.Should().Be(repId);
    }
}
