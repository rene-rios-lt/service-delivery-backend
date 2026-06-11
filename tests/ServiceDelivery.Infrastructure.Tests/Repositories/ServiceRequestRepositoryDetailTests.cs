using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class ServiceRequestRepositoryDetailTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ServiceRequestDetailTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static User Requester(Guid id, string name, Guid dealerId)
        => new() { Id = id, Name = name, Email = $"{id}@example.com", PasswordHash = "x", Role = UserRole.Requester, Tier = ServiceTier.Gold, DealerId = dealerId };

    private static User Rep(Guid id, string name, Guid dealerId)
        => new() { Id = id, Name = name, Email = $"{id}@dealer.com", PasswordHash = "x", Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = dealerId };

    private static DiagnosticTroubleCode Dtc(Guid id, string title, Guid dealerId)
        => new() { Id = id, DealerId = dealerId, Code = "DTC-001", HumanReadableTitle = title, RequiredEquipmentType = EquipmentType.HydraulicTool };

    [Fact]
    public async Task GivenASeededRequestWithOffers_WhenGetDetailByIdAsyncCalled_ThenReturnsDetailWithRequesterDtcRepAndOfferHistory()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var offeredAt = new DateTime(2026, 6, 1, 9, 5, 0, DateTimeKind.Utc);
        var expiresAt = new DateTime(2026, 6, 1, 9, 10, 0, DateTimeKind.Utc);

        context.Users.Add(Requester(requesterId, "Gold User 1", dealerId));
        context.Users.Add(Rep(repId, "Rep One", dealerId));
        context.DiagnosticTroubleCodes.Add(Dtc(dtcId, "Hydraulic system fault", dealerId));
        context.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = dealerId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Assigned,
            AssignedRepId = repId,
            Latitude = 12.5,
            Longitude = -34.25,
            CreatedAt = createdAt
        });
        context.JobOffers.Add(new JobOffer
        {
            Id = offerId,
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = offeredAt,
            ExpiresAt = expiresAt,
            Status = JobOfferStatus.Accepted
        });
        await context.SaveChangesAsync();

        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetDetailByIdAsync(requestId, dealerId);

        // Assert
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(requestId);
        result.RequesterId.Should().Be(requesterId);
        result.RequesterName.Should().Be("Gold User 1");
        result.Tier.Should().Be(ServiceTier.Gold);
        result.DtcTitle.Should().Be("Hydraulic system fault");
        result.Latitude.Should().Be(12.5);
        result.Longitude.Should().Be(-34.25);
        result.Status.Should().Be(ServiceRequestStatus.Assigned);
        result.AssignedRepId.Should().Be(repId);
        result.AssignedRepName.Should().Be("Rep One");
        result.CreatedAt.Should().Be(createdAt);
        result.OfferHistory.Should().HaveCount(1);
        result.OfferHistory[0].OfferId.Should().Be(offerId);
        result.OfferHistory[0].RepId.Should().Be(repId);
        result.OfferHistory[0].RepName.Should().Be("Rep One");
        result.OfferHistory[0].Status.Should().Be(JobOfferStatus.Accepted);
        result.OfferHistory[0].OfferedAt.Should().Be(offeredAt);
        result.OfferHistory[0].ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GivenAnUnassignedSeededRequest_WhenGetDetailByIdAsyncCalled_ThenAssignedRepNameIsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        context.Users.Add(Requester(requesterId, "Bronze User 1", dealerId));
        context.DiagnosticTroubleCodes.Add(Dtc(dtcId, "Electrical fault", dealerId));
        context.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = dealerId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = ServiceTier.Bronze,
            Status = ServiceRequestStatus.Pending,
            AssignedRepId = null,
            Latitude = 1.0,
            Longitude = 2.0,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetDetailByIdAsync(requestId, dealerId);

        // Assert
        result.Should().NotBeNull();
        result!.AssignedRepId.Should().BeNull();
        result.AssignedRepName.Should().BeNull();
        result.OfferHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenASeededRequestWithUnorderedOffers_WhenGetDetailByIdAsyncCalled_ThenOfferHistoryIsAscendingByOfferedAt()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var rep1 = Guid.NewGuid();
        var rep2 = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var earliest = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var middle = new DateTime(2026, 6, 1, 9, 30, 0, DateTimeKind.Utc);
        var latest = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var earliestOffer = Guid.NewGuid();
        var middleOffer = Guid.NewGuid();
        var latestOffer = Guid.NewGuid();

        context.Users.Add(Requester(requesterId, "Silver User 1", dealerId));
        context.Users.Add(Rep(rep1, "Rep One", dealerId));
        context.Users.Add(Rep(rep2, "Rep Two", dealerId));
        context.DiagnosticTroubleCodes.Add(Dtc(dtcId, "Braking fault", dealerId));
        context.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = dealerId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = ServiceTier.Silver,
            Status = ServiceRequestStatus.Pending,
            AssignedRepId = null,
            Latitude = 3.0,
            Longitude = 4.0,
            CreatedAt = DateTime.UtcNow
        });
        context.JobOffers.AddRange(
            new JobOffer { Id = latestOffer, ServiceRequestId = requestId, RepId = rep2, OfferedAt = latest, ExpiresAt = latest.AddMinutes(5), Status = JobOfferStatus.Pending },
            new JobOffer { Id = earliestOffer, ServiceRequestId = requestId, RepId = rep1, OfferedAt = earliest, ExpiresAt = earliest.AddMinutes(5), Status = JobOfferStatus.Declined },
            new JobOffer { Id = middleOffer, ServiceRequestId = requestId, RepId = rep2, OfferedAt = middle, ExpiresAt = middle.AddMinutes(5), Status = JobOfferStatus.Expired });
        await context.SaveChangesAsync();

        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetDetailByIdAsync(requestId, dealerId);

        // Assert
        result.Should().NotBeNull();
        result!.OfferHistory.Should().HaveCount(3);
        result.OfferHistory.Select(o => o.OfferedAt).Should().BeInAscendingOrder();
        result.OfferHistory[0].OfferId.Should().Be(earliestOffer);
        result.OfferHistory[1].OfferId.Should().Be(middleOffer);
        result.OfferHistory[2].OfferId.Should().Be(latestOffer);
    }

    [Fact]
    public async Task GivenARequestInAnotherDealer_WhenGetDetailByIdAsyncCalledWithCallerDealer_ThenReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var requestDealerId = Guid.NewGuid();
        var callerDealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        context.Users.Add(Requester(requesterId, "Gold User 1", requestDealerId));
        context.DiagnosticTroubleCodes.Add(Dtc(dtcId, "Hydraulic system fault", requestDealerId));
        context.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = requestDealerId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Pending,
            Latitude = 1.0,
            Longitude = 2.0,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetDetailByIdAsync(requestId, callerDealerId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenNoRequestWithId_WhenGetDetailByIdAsyncCalled_ThenReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new ServiceRequestRepository(context);

        // Act
        var result = await repository.GetDetailByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }
}
