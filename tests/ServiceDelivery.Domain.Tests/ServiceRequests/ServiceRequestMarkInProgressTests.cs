using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.ServiceRequests;

public class ServiceRequestMarkInProgressTests
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
            CreatedAt = DateTime.UtcNow
        };

    [Fact]
    public void GivenAnAssignedRequest_WhenMarkInProgress_ThenStatusIsInProgress()
    {
        // Arrange
        var request = RequestWithStatus(ServiceRequestStatus.Assigned);

        // Act
        request.MarkInProgress();

        // Assert
        request.Status.Should().Be(ServiceRequestStatus.InProgress);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending)]
    [InlineData(ServiceRequestStatus.InProgress)]
    [InlineData(ServiceRequestStatus.Completed)]
    public void GivenANonAssignedRequest_WhenMarkInProgress_ThenInvalidServiceRequestStateExceptionIsThrown(ServiceRequestStatus status)
    {
        // Arrange
        var request = RequestWithStatus(status);

        // Act
        var act = () => request.MarkInProgress();

        // Assert
        act.Should().Throw<InvalidServiceRequestStateException>();
        request.Status.Should().Be(status);
    }
}
