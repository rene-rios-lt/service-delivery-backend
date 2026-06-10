using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.ServiceRequests.Queries;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Application.Tests.Features.ServiceRequests;

public class GetActiveServiceRequestsQueryHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _repositoryMock;
    private readonly GetActiveServiceRequestsQueryHandler _handler;

    public GetActiveServiceRequestsQueryHandlerTests()
    {
        _repositoryMock = new Mock<IServiceRequestRepository>();
        _handler = new GetActiveServiceRequestsQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task GivenMixedStatusRequestsForDealer_WhenGetActiveServiceRequestsHandled_ThenOnlyNonCompletedAreReturned()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var activeRequests = new List<ServiceRequestSummary>
        {
            new(Guid.NewGuid(), "Requester One", ServiceTier.Bronze, "Hydraulic fault", ServiceRequestStatus.Pending, null, null, DateTime.UtcNow),
            new(Guid.NewGuid(), "Requester Two", ServiceTier.Silver, "Electrical fault", ServiceRequestStatus.Assigned, Guid.NewGuid(), "Rep One", DateTime.UtcNow),
        };

        _repositoryMock
            .Setup(r => r.GetActiveByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeRequests);

        var query = new GetActiveServiceRequestsQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContain(dto => dto.Status == ServiceRequestStatus.Completed.ToString());
    }

    [Fact]
    public async Task GivenRequestsForTwoDealers_WhenGetActiveServiceRequestsHandled_ThenOnlyCallerDealerRequestsAreReturned()
    {
        // Arrange
        var callerDealerId = Guid.NewGuid();
        var otherDealerId = Guid.NewGuid();

        var callerDealerRequests = new List<ServiceRequestSummary>
        {
            new(Guid.NewGuid(), "Requester A", ServiceTier.Gold, "Braking fault", ServiceRequestStatus.Pending, null, null, DateTime.UtcNow),
        };

        _repositoryMock
            .Setup(r => r.GetActiveByDealerIdAsync(callerDealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerDealerRequests);

        _repositoryMock
            .Setup(r => r.GetActiveByDealerIdAsync(otherDealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceRequestSummary>
            {
                new(Guid.NewGuid(), "Requester B", ServiceTier.Bronze, "Fuel fault", ServiceRequestStatus.InProgress, Guid.NewGuid(), "Rep X", DateTime.UtcNow),
            });

        var query = new GetActiveServiceRequestsQuery(callerDealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].RequesterName.Should().Be("Requester A");
    }

    [Fact]
    public async Task GivenAnActiveRequest_WhenGetActiveServiceRequestsHandled_ThenDtoContainsAllRequiredFields()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        var summaries = new List<ServiceRequestSummary>
        {
            new(requestId, "Gold User 1", ServiceTier.Gold, "Hydraulic system fault", ServiceRequestStatus.Assigned, repId, "Rep One", createdAt),
        };

        _repositoryMock
            .Setup(r => r.GetActiveByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);

        var query = new GetActiveServiceRequestsQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.RequestId.Should().Be(requestId);
        dto.RequesterName.Should().Be("Gold User 1");
        dto.Tier.Should().Be("Gold");
        dto.DtcTitle.Should().Be("Hydraulic system fault");
        dto.Status.Should().Be("Assigned");
        dto.AssignedRepId.Should().Be(repId);
        dto.AssignedRepName.Should().Be("Rep One");
        dto.CreatedAt.Should().Be(createdAt);
    }
}
