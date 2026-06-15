using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.ServiceRequests;

public class ServiceRequestMarkCompletedTests
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
    public void GivenAnInProgressRequest_WhenMarkCompleted_ThenStatusIsCompleted()
    {
        // Arrange
        var request = RequestWithStatus(ServiceRequestStatus.InProgress);

        // Act
        request.MarkCompleted();

        // Assert
        request.Status.Should().Be(ServiceRequestStatus.Completed);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending)]
    [InlineData(ServiceRequestStatus.Assigned)]
    [InlineData(ServiceRequestStatus.Completed)]
    public void GivenARequestNotInProgress_WhenMarkCompleted_ThenInvalidServiceRequestStateExceptionIsThrown(ServiceRequestStatus status)
    {
        // Arrange
        var request = RequestWithStatus(status);

        // Act
        var act = () => request.MarkCompleted();

        // Assert
        act.Should().Throw<InvalidServiceRequestStateException>();
        request.Status.Should().Be(status);
    }
}
