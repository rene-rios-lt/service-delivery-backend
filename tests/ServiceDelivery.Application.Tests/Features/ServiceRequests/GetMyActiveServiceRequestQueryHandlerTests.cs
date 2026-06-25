using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.ServiceRequests.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.ServiceRequests;

public class GetMyActiveServiceRequestQueryHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepositoryMock;
    private readonly Mock<IDiagnosticTroubleCodeRepository> _dtcRepositoryMock;
    private readonly Mock<IRepStateRepository> _repStateRepositoryMock;
    private readonly GetMyActiveServiceRequestQueryHandler _handler;

    public GetMyActiveServiceRequestQueryHandlerTests()
    {
        _serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        _dtcRepositoryMock = new Mock<IDiagnosticTroubleCodeRepository>();
        _repStateRepositoryMock = new Mock<IRepStateRepository>();
        _handler = new GetMyActiveServiceRequestQueryHandler(
            _serviceRequestRepositoryMock.Object,
            _dtcRepositoryMock.Object,
            _repStateRepositoryMock.Object);
    }

    [Fact]
    public async Task GivenARepWithAnAssignedRequest_WhenGetMyActiveServiceRequestHandled_ThenReturnsActiveRequest()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            AssignedRepId = repId,
            DtcId = dtcId,
            Status = ServiceRequestStatus.Assigned,
            Tier = ServiceTier.Bronze,
            Latitude = 41.5,
            Longitude = -93.6,
            CreatedAt = DateTime.UtcNow
        };

        _serviceRequestRepositoryMock
            .Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceRequest);

        _dtcRepositoryMock
            .Setup(r => r.GetByIdAsync(dtcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticTroubleCode { Id = dtcId, HumanReadableTitle = "Hydraulic fault" });

        var query = new GetMyActiveServiceRequestQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(requestId);
        result.Status.Should().Be("Assigned");
    }

    [Fact]
    public async Task GivenARepWithAnInProgressRequest_WhenGetMyActiveServiceRequestHandled_ThenReturnsActiveRequest()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            AssignedRepId = repId,
            DtcId = dtcId,
            Status = ServiceRequestStatus.InProgress,
            Tier = ServiceTier.Silver,
            Latitude = 42.0,
            Longitude = -91.5,
            CreatedAt = DateTime.UtcNow
        };

        _serviceRequestRepositoryMock
            .Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceRequest);

        _dtcRepositoryMock
            .Setup(r => r.GetByIdAsync(dtcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticTroubleCode { Id = dtcId, HumanReadableTitle = "Electrical fault" });

        var query = new GetMyActiveServiceRequestQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(requestId);
        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task GivenARepWithAnAssignedRequest_WhenGetMyActiveServiceRequestHandled_ThenDtoContainsAllRequiredFields()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            AssignedRepId = repId,
            DtcId = dtcId,
            Status = ServiceRequestStatus.Assigned,
            Tier = ServiceTier.Gold,
            Latitude = 41.878,
            Longitude = -93.097,
            CreatedAt = createdAt
        };

        _serviceRequestRepositoryMock
            .Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceRequest);

        _dtcRepositoryMock
            .Setup(r => r.GetByIdAsync(dtcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticTroubleCode { Id = dtcId, HumanReadableTitle = "Hydraulic system fault" });

        var query = new GetMyActiveServiceRequestQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(requestId);
        result.Tier.Should().Be("Gold");
        result.DtcTitle.Should().Be("Hydraulic system fault");
        result.Status.Should().Be("Assigned");
        result.RequesterLatitude.Should().Be(41.878);
        result.RequesterLongitude.Should().Be(-93.097);
        result.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task GivenARepWithinFifteenMiles_WhenGetMyActiveServiceRequestHandled_ThenRepStateReflectsProximityNotRequestStatus()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();

        _serviceRequestRepositoryMock
            .Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceRequest
            {
                Id = Guid.NewGuid(),
                AssignedRepId = repId,
                DtcId = dtcId,
                Status = ServiceRequestStatus.Assigned,
                Tier = ServiceTier.Gold,
                Latitude = 41.88,
                Longitude = -93.10,
                CreatedAt = DateTime.UtcNow
            });
        _dtcRepositoryMock
            .Setup(r => r.GetByIdAsync(dtcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticTroubleCode { Id = dtcId, HumanReadableTitle = "Hydraulic system fault" });
        _repStateRepositoryMock
            .Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepStateRecord { RepId = repId, State = RepState.Within15Miles });

        var query = new GetMyActiveServiceRequestQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — RepState carries the rep's proximity (drives the "I've Arrived" enable rule),
        // distinct from the request lifecycle Status.
        result.Should().NotBeNull();
        result!.RepState.Should().Be("Within15Miles");
        result.Status.Should().Be("Assigned");
    }

    [Fact]
    public async Task GivenARepWithNoActiveRequest_WhenGetMyActiveServiceRequestHandled_ThenReturnsNull()
    {
        // Arrange
        var repId = Guid.NewGuid();

        _serviceRequestRepositoryMock
            .Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var query = new GetMyActiveServiceRequestQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
