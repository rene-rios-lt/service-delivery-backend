using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.ServiceRequests;

public class ServiceRequestAssignTests
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
    public void GivenAPendingRequest_WhenAssignedToRep_ThenStatusIsAssignedAndAssignedRepIdIsSet()
    {
        // Arrange
        var request = RequestWithStatus(ServiceRequestStatus.Pending);
        var repId = Guid.NewGuid();

        // Act
        request.AssignTo(repId);

        // Assert
        request.Status.Should().Be(ServiceRequestStatus.Assigned);
        request.AssignedRepId.Should().Be(repId);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Assigned)]
    [InlineData(ServiceRequestStatus.InProgress)]
    [InlineData(ServiceRequestStatus.Completed)]
    public void GivenANonPendingRequest_WhenAssignedToRep_ThenInvalidJobOfferStateExceptionIsThrown(ServiceRequestStatus status)
    {
        // Arrange
        var request = RequestWithStatus(status);

        // Act
        var act = () => request.AssignTo(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidJobOfferStateException>();
    }
}
