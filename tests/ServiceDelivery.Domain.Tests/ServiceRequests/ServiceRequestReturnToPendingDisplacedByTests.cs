using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.ServiceRequests;

public class ServiceRequestReturnToPendingDisplacedByTests
{
    private static ServiceRequest RequestWithStatus(ServiceRequestStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            DealerId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            DtcId = Guid.NewGuid(),
            Status = status,
            Tier = ServiceTier.Silver,
            AssignedRepId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

    [Theory]
    [InlineData(ServiceRequestStatus.Assigned)]
    [InlineData(ServiceRequestStatus.InProgress)]
    public void GivenAnAssignedOrInProgressRequest_WhenReturnToPendingDisplacedBy_ThenStatusIsPendingAndDisplacementStamped(
        ServiceRequestStatus status)
    {
        // Arrange
        var request = RequestWithStatus(status);
        var displacedRepId = Guid.NewGuid();

        // Act
        request.ReturnToPendingDisplacedBy(displacedRepId);

        // Assert
        request.Status.Should().Be(ServiceRequestStatus.Pending);
        request.AssignedRepId.Should().BeNull();
        request.DisplacedFromRepId.Should().Be(displacedRepId);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending)]
    [InlineData(ServiceRequestStatus.Completed)]
    public void GivenANonActiveRequest_WhenReturnToPendingDisplacedBy_ThenInvalidServiceRequestStateExceptionIsThrown(
        ServiceRequestStatus status)
    {
        // Arrange
        var request = RequestWithStatus(status);

        // Act
        var act = () => request.ReturnToPendingDisplacedBy(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidServiceRequestStateException>();
        request.DisplacedFromRepId.Should().BeNull();
    }

    [Fact]
    public void GivenADisplacedRequest_WhenClearDisplacement_ThenDisplacedFromRepIdIsNull()
    {
        // Arrange
        var request = RequestWithStatus(ServiceRequestStatus.Assigned);
        request.ReturnToPendingDisplacedBy(Guid.NewGuid());

        // Act
        request.ClearDisplacement();

        // Assert
        request.DisplacedFromRepId.Should().BeNull();
    }
}
