using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Features.ServiceRequests.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.ServiceRequests;

public class SubmitServiceRequestCommandHandlerTests
{
    private readonly Mock<IServiceRequestRepository> _repositoryMock;
    private readonly Mock<IMatchingService> _matchingServiceMock;
    private readonly SubmitServiceRequestCommandHandler _handler;

    public SubmitServiceRequestCommandHandlerTests()
    {
        _repositoryMock = new Mock<IServiceRequestRepository>();
        _matchingServiceMock = new Mock<IMatchingService>();
        _handler = new SubmitServiceRequestCommandHandler(_repositoryMock.Object, _matchingServiceMock.Object);
    }

    [Fact]
    public async Task GivenAValidSubmitCommand_WhenHandled_ThenServiceRequestIsPersisted()
    {
        // Arrange
        var command = new SubmitServiceRequestCommand(
            RequesterId: Guid.NewGuid(),
            DealerId: Guid.NewGuid(),
            Tier: ServiceTier.Bronze,
            DtcId: Guid.NewGuid(),
            Latitude: 37.7749,
            Longitude: -122.4194);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAValidSubmitCommand_WhenHandled_ThenPersistedRequestHasStatusPending()
    {
        // Arrange
        ServiceRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        var command = new SubmitServiceRequestCommand(
            RequesterId: Guid.NewGuid(),
            DealerId: Guid.NewGuid(),
            Tier: ServiceTier.Silver,
            DtcId: Guid.NewGuid(),
            Latitude: 37.7749,
            Longitude: -122.4194);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(ServiceRequestStatus.Pending);
    }

    [Fact]
    public async Task GivenAValidSubmitCommand_WhenHandled_ThenPersistedRequestHasDealerIdFromCommand()
    {
        // Arrange
        ServiceRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        var dealerId = Guid.NewGuid();
        var command = new SubmitServiceRequestCommand(
            RequesterId: Guid.NewGuid(),
            DealerId: dealerId,
            Tier: ServiceTier.Gold,
            DtcId: Guid.NewGuid(),
            Latitude: 37.7749,
            Longitude: -122.4194);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.DealerId.Should().Be(dealerId);
    }

    [Fact]
    public async Task GivenAValidSubmitCommand_WhenHandled_ThenPersistedRequestHasTierFromCommand()
    {
        // Arrange
        ServiceRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        var command = new SubmitServiceRequestCommand(
            RequesterId: Guid.NewGuid(),
            DealerId: Guid.NewGuid(),
            Tier: ServiceTier.Gold,
            DtcId: Guid.NewGuid(),
            Latitude: 37.7749,
            Longitude: -122.4194);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Tier.Should().Be(ServiceTier.Gold);
    }

    [Fact]
    public async Task GivenAValidSubmitCommand_WhenHandled_ThenMatchingServiceIsInvokedWithNewRequestId()
    {
        // Arrange
        Guid capturedRequestId = Guid.Empty;
        ServiceRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        _matchingServiceMock
            .Setup(m => m.RunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => capturedRequestId = id)
            .Returns(Task.CompletedTask);

        var command = new SubmitServiceRequestCommand(
            RequesterId: Guid.NewGuid(),
            DealerId: Guid.NewGuid(),
            Tier: ServiceTier.Bronze,
            DtcId: Guid.NewGuid(),
            Latitude: 37.7749,
            Longitude: -122.4194);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _matchingServiceMock.Verify(m => m.RunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedRequest.Should().NotBeNull();
        capturedRequestId.Should().Be(capturedRequest!.Id);
    }
}
