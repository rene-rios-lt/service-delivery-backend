using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.JobOffers;

public class JobOfferExpireTests
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
    public void GivenAPendingJobOffer_WhenExpired_ThenStatusIsExpired()
    {
        // Arrange
        var offer = OfferWithStatus(JobOfferStatus.Pending);

        // Act
        offer.Expire();

        // Assert
        offer.Status.Should().Be(JobOfferStatus.Expired);
    }

    [Theory]
    [InlineData(JobOfferStatus.Accepted)]
    [InlineData(JobOfferStatus.Declined)]
    [InlineData(JobOfferStatus.Expired)]
    public void GivenANonPendingJobOffer_WhenExpired_ThenInvalidJobOfferStateExceptionIsThrown(JobOfferStatus status)
    {
        // Arrange
        var offer = OfferWithStatus(status);

        // Act
        var act = () => offer.Expire();

        // Assert
        act.Should().Throw<InvalidJobOfferStateException>();
    }
}
