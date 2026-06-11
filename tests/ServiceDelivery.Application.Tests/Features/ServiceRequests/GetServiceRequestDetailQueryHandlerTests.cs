using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.ServiceRequests.Queries;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Application.Tests.Features.ServiceRequests;

public class GetServiceRequestDetailQueryHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _repositoryMock;
    private readonly GetServiceRequestDetailQueryHandler _handler;

    public GetServiceRequestDetailQueryHandlerTests()
    {
        _repositoryMock = new Mock<IServiceRequestRepository>();
        _handler = new GetServiceRequestDetailQueryHandler(_repositoryMock.Object);
    }

    private void SetupDetail(Guid requestId, Guid dealerId, ServiceRequestDetail detail)
    {
        _repositoryMock
            .Setup(r => r.GetDetailByIdAsync(requestId, dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);
    }

    [Fact]
    public async Task GivenARequestWithAssignedRepAndOffers_WhenDetailHandled_ThenDtoContainsAllRequiredFields()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var offeredAt = new DateTime(2026, 6, 1, 10, 5, 0, DateTimeKind.Utc);
        var expiresAt = new DateTime(2026, 6, 1, 10, 10, 0, DateTimeKind.Utc);

        var detail = new ServiceRequestDetail(
            requestId,
            requesterId,
            "Gold User 1",
            ServiceTier.Gold,
            "Hydraulic system fault",
            12.5,
            -34.25,
            ServiceRequestStatus.Assigned,
            repId,
            "Rep One",
            createdAt,
            new List<JobOfferHistoryEntry>
            {
                new(offerId, repId, "Rep One", JobOfferStatus.Accepted, offeredAt, expiresAt)
            });
        SetupDetail(requestId, dealerId, detail);

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, requesterId, UserRole.Dispatcher);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(requestId);
        result.RequesterName.Should().Be("Gold User 1");
        result.Tier.Should().Be("Gold");
        result.DtcTitle.Should().Be("Hydraulic system fault");
        result.RequesterLocation.Lat.Should().Be(12.5);
        result.RequesterLocation.Lng.Should().Be(-34.25);
        result.Status.Should().Be("Assigned");
        result.AssignedRep.Should().NotBeNull();
        result.AssignedRep!.RepId.Should().Be(repId);
        result.AssignedRep.Name.Should().Be("Rep One");
        result.CreatedAt.Should().Be(createdAt);
        result.OfferHistory.Should().HaveCount(1);
        result.OfferHistory[0].OfferId.Should().Be(offerId);
        result.OfferHistory[0].RepId.Should().Be(repId);
        result.OfferHistory[0].RepName.Should().Be("Rep One");
        result.OfferHistory[0].Status.Should().Be("Accepted");
        result.OfferHistory[0].OfferedAt.Should().Be(offeredAt);
        result.OfferHistory[0].ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GivenAnUnassignedRequest_WhenDetailHandled_ThenAssignedRepIsNull()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var detail = new ServiceRequestDetail(
            requestId,
            requesterId,
            "Bronze User 1",
            ServiceTier.Bronze,
            "Electrical fault",
            1.0,
            2.0,
            ServiceRequestStatus.Pending,
            null,
            null,
            DateTime.UtcNow,
            new List<JobOfferHistoryEntry>());
        SetupDetail(requestId, dealerId, detail);

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, requesterId, UserRole.Dispatcher);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.AssignedRep.Should().BeNull();
    }

    [Fact]
    public async Task GivenARequestWithMultipleOffers_WhenDetailHandled_ThenOfferHistoryIsOrderedByOfferedAtAscending()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var rep1 = Guid.NewGuid();
        var rep2 = Guid.NewGuid();
        var earliest = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var latest = new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc);
        var offer1 = Guid.NewGuid();
        var offer2 = Guid.NewGuid();

        var detail = new ServiceRequestDetail(
            requestId,
            requesterId,
            "Silver User 1",
            ServiceTier.Silver,
            "Braking fault",
            3.0,
            4.0,
            ServiceRequestStatus.Pending,
            null,
            null,
            DateTime.UtcNow,
            new List<JobOfferHistoryEntry>
            {
                new(offer1, rep1, "Rep One", JobOfferStatus.Declined, earliest, earliest.AddMinutes(5)),
                new(offer2, rep2, "Rep Two", JobOfferStatus.Pending, latest, latest.AddMinutes(5))
            });
        SetupDetail(requestId, dealerId, detail);

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, requesterId, UserRole.Dispatcher);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OfferHistory.Should().HaveCount(2);
        result.OfferHistory.Select(o => o.OfferedAt).Should().BeInAscendingOrder();
        result.OfferHistory[0].OfferId.Should().Be(offer1);
        result.OfferHistory[1].OfferId.Should().Be(offer2);
    }

    [Fact]
    public async Task GivenARequestWithOffers_WhenDetailHandled_ThenTierStatusAndOfferStatusAreStrings()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var repId = Guid.NewGuid();

        var detail = new ServiceRequestDetail(
            requestId,
            requesterId,
            "Gold User 1",
            ServiceTier.Gold,
            "Hydraulic system fault",
            5.0,
            6.0,
            ServiceRequestStatus.InProgress,
            repId,
            "Rep One",
            DateTime.UtcNow,
            new List<JobOfferHistoryEntry>
            {
                new(Guid.NewGuid(), repId, "Rep One", JobOfferStatus.Accepted, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5))
            });
        SetupDetail(requestId, dealerId, detail);

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, requesterId, UserRole.Dispatcher);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Tier.Should().Be("Gold");
        result.Status.Should().Be("InProgress");
        result.OfferHistory[0].Status.Should().Be("Accepted");
    }

    private static ServiceRequestDetail BuildDetail(
        Guid requestId,
        Guid requesterId,
        Guid? assignedRepId)
        => new(
            requestId,
            requesterId,
            "Requester",
            ServiceTier.Gold,
            "Hydraulic system fault",
            1.0,
            2.0,
            assignedRepId is null ? ServiceRequestStatus.Pending : ServiceRequestStatus.Assigned,
            assignedRepId,
            assignedRepId is null ? null : "Rep One",
            DateTime.UtcNow,
            new List<JobOfferHistoryEntry>());

    [Fact]
    public async Task GivenADispatcherCaller_WhenDetailHandledForInDealerRequest_ThenReturnsDetail()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        SetupDetail(requestId, dealerId, BuildDetail(requestId, requesterId, null));

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, Guid.NewGuid(), UserRole.Dispatcher);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GivenARequesterCaller_WhenDetailHandledForOwnRequest_ThenReturnsDetail()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        SetupDetail(requestId, dealerId, BuildDetail(requestId, requesterId, null));

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, requesterId, UserRole.Requester);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GivenARequesterCaller_WhenDetailHandledForAnotherRequestersRequest_ThenReturnsNull()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var ownerRequesterId = Guid.NewGuid();
        var otherRequesterId = Guid.NewGuid();
        SetupDetail(requestId, dealerId, BuildDetail(requestId, ownerRequesterId, null));

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, otherRequesterId, UserRole.Requester);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenAServiceRepCaller_WhenDetailHandledForAssignedRequest_ThenReturnsDetail()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        SetupDetail(requestId, dealerId, BuildDetail(requestId, requesterId, repId));

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, repId, UserRole.ServiceRep);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GivenAServiceRepCaller_WhenDetailHandledForUnassignedToThemRequest_ThenReturnsNull()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var assignedRepId = Guid.NewGuid();
        var callingRepId = Guid.NewGuid();
        SetupDetail(requestId, dealerId, BuildDetail(requestId, requesterId, assignedRepId));

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, callingRepId, UserRole.ServiceRep);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenANonExistentId_WhenDetailHandled_ThenReturnsNull()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetDetailByIdAsync(requestId, dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequestDetail?)null);

        var query = new GetServiceRequestDetailQuery(requestId, dealerId, Guid.NewGuid(), UserRole.Dispatcher);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
