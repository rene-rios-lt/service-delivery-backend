using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.JobOffers;

public class JobOfferDeclineTests
{
    private static JobOffer OfferWithStatus(JobOfferStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            RepId = Guid.NewGuid(),
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = status
        };

    [Fact]
    public void GivenAPendingJobOffer_WhenDeclined_ThenStatusIsDeclined()
    {
        // Arrange
        var offer = OfferWithStatus(JobOfferStatus.Pending);

        // Act
        offer.Decline();

        // Assert
        offer.Status.Should().Be(JobOfferStatus.Declined);
    }

    [Theory]
    [InlineData(JobOfferStatus.Accepted)]
    [InlineData(JobOfferStatus.Declined)]
    [InlineData(JobOfferStatus.Expired)]
    public void GivenANonPendingJobOffer_WhenDeclined_ThenInvalidJobOfferStateExceptionIsThrown(JobOfferStatus status)
    {
        // Arrange
        var offer = OfferWithStatus(status);

        // Act
        var act = () => offer.Decline();

        // Assert
        act.Should().Throw<InvalidJobOfferStateException>();
    }
}
