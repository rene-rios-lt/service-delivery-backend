using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.ServiceRequests;

public class ServiceRequestReturnToPendingTests
{
    private static ServiceRequest RequestWithStatus(ServiceRequestStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            DealerId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            DtcId = Guid.NewGuid(),
            Latitude = 41.6,
            Longitude = -93.6,
            Status = status,
            Tier = ServiceTier.Gold,
            AssignedRepId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

    [Fact]
    public void GivenAnAssignedRequest_WhenReturnToPending_ThenStatusIsPendingAndRepCleared()
    {
        // Arrange
        var request = RequestWithStatus(ServiceRequestStatus.Assigned);

        // Act
        request.ReturnToPending();

        // Assert
        request.Status.Should().Be(ServiceRequestStatus.Pending);
        request.AssignedRepId.Should().BeNull();
    }

    [Fact]
    public void GivenAnInProgressRequest_WhenReturnToPending_ThenStatusIsPending()
    {
        // Arrange
        var request = RequestWithStatus(ServiceRequestStatus.InProgress);

        // Act
        request.ReturnToPending();

        // Assert
        request.Status.Should().Be(ServiceRequestStatus.Pending);
        request.AssignedRepId.Should().BeNull();
    }

    [Fact]
    public void GivenAPendingRequest_WhenReturnToPending_ThenInvalidServiceRequestStateExceptionIsThrown()
    {
        // Arrange
        var request = RequestWithStatus(ServiceRequestStatus.Pending);

        // Act
        var act = request.ReturnToPending;

        // Assert
        act.Should().Throw<InvalidServiceRequestStateException>();
    }
}
