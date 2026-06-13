using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Features.JobOffers.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.JobOffers;

public class DeclineJobOfferCommandHandlerTests
{
    private readonly Mock<IJobOfferRepository> _jobOfferRepository = new();
    private readonly Mock<IMatchingService> _matchingService = new();
    private readonly DeclineJobOfferCommandHandler _handler;

    private static readonly Guid OfferId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid RepId = Guid.NewGuid();

    public DeclineJobOfferCommandHandlerTests()
    {
        _handler = new DeclineJobOfferCommandHandler(
            _jobOfferRepository.Object,
            _matchingService.Object);
    }

    private JobOffer SetupPendingOffer()
    {
        var offer = new JobOffer
        {
            Id = OfferId,
            ServiceRequestId = RequestId,
            RepId = RepId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = JobOfferStatus.Pending
        };
        _jobOfferRepository.Setup(r => r.GetByIdAsync(OfferId, It.IsAny<CancellationToken>())).ReturnsAsync(offer);
        return offer;
    }

    private DeclineJobOfferCommand Command() => new(OfferId, RepId);

    [Fact]
    public async Task GivenADeclinedOffer_WhenHandled_ThenOfferIsPersistedAsDeclinedBeforeRematch()
    {
        // Arrange
        SetupPendingOffer();
        var sequence = new List<string>();
        _jobOfferRepository
            .Setup(r => r.UpdateAsync(It.Is<JobOffer>(o => o.Status == JobOfferStatus.Declined), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("update"))
            .Returns(Task.CompletedTask);
        _matchingService
            .Setup(m => m.RunAsync(RequestId, It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("rematch"))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        sequence.Should().Equal("update", "rematch");
    }

    [Fact]
    public async Task GivenADeclinedOffer_WhenHandled_ThenMatchingServiceRunAsyncIsInvokedForTheRequest()
    {
        // Arrange
        SetupPendingOffer();

        // Act
        await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        _matchingService.Verify(m => m.RunAsync(RequestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenADeclinedOffer_WhenHandled_ThenResultReflectsDeclinedStatus()
    {
        // Arrange
        SetupPendingOffer();

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.OfferId.Should().Be(OfferId);
        result.RequestId.Should().Be(RequestId);
        result.OfferStatus.Should().Be(JobOfferStatus.Declined.ToString());
    }

    [Fact]
    public async Task GivenNoEligibleRepRemains_WhenOfferDeclined_ThenDeclineStillSucceeds()
    {
        // Arrange
        SetupPendingOffer();
        _matchingService
            .Setup(m => m.RunAsync(RequestId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.OfferStatus.Should().Be(JobOfferStatus.Declined.ToString());
        _jobOfferRepository.Verify(r => r.UpdateAsync(
            It.Is<JobOffer>(o => o.Status == JobOfferStatus.Declined), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(JobOfferStatus.Accepted)]
    [InlineData(JobOfferStatus.Declined)]
    [InlineData(JobOfferStatus.Expired)]
    public async Task GivenANonPendingOffer_WhenHandled_ThenInvalidJobOfferStateExceptionIsThrownAndNoRematch(JobOfferStatus status)
    {
        // Arrange
        var offer = new JobOffer
        {
            Id = OfferId,
            ServiceRequestId = RequestId,
            RepId = RepId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = status
        };
        _jobOfferRepository.Setup(r => r.GetByIdAsync(OfferId, It.IsAny<CancellationToken>())).ReturnsAsync(offer);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidJobOfferStateException>();
        _jobOfferRepository.Verify(r => r.UpdateAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Never);
        _matchingService.Verify(m => m.RunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAnUnknownOfferId_WhenHandled_ThenKeyNotFoundExceptionIsThrown()
    {
        // Arrange
        _jobOfferRepository.Setup(r => r.GetByIdAsync(OfferId, It.IsAny<CancellationToken>())).ReturnsAsync((JobOffer?)null);

        // Act
        var act = () => _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
        _matchingService.Verify(m => m.RunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
