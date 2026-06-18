using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Rep.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Rep;

public class HeartbeatCommandHandlerTests
{
    private readonly Mock<IRepStateRepository> _repStateRepoMock = new();
    private readonly HeartbeatCommandHandler _handler;

    public HeartbeatCommandHandlerTests()
    {
        _handler = new HeartbeatCommandHandler(_repStateRepoMock.Object);
    }

    [Fact]
    public async Task GivenAHumanControlledRep_WhenHeartbeatHandled_ThenLastHeartbeatAtIsUpdatedToNow()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var repState = new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            HumanControlled = true,
            LastHeartbeatAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        var before = DateTime.UtcNow;

        // Act
        var result = await _handler.Handle(new HeartbeatCommand(repId), CancellationToken.None);

        // Assert
        repState.LastHeartbeatAt.Should().NotBeNull();
        repState.LastHeartbeatAt!.Value.Should().BeOnOrAfter(before);
        result.RepId.Should().Be(repId);
        result.LastHeartbeatAt.Should().Be(repState.LastHeartbeatAt!.Value);
    }

    [Fact]
    public async Task GivenAHumanControlledRep_WhenHeartbeatHandled_ThenRepStateIsUpserted()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available, HumanControlled = true };
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);

        // Act
        await _handler.Handle(new HeartbeatCommand(repId), CancellationToken.None);

        // Assert
        _repStateRepoMock.Verify(r => r.UpsertAsync(
            It.Is<RepStateRecord>(s => s.RepId == repId && s.LastHeartbeatAt != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoRepStateRecord_WhenHeartbeatHandled_ThenThrowsKeyNotFoundException()
    {
        // Arrange
        var repId = Guid.NewGuid();
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepStateRecord?)null);

        // Act
        var act = () => _handler.Handle(new HeartbeatCommand(repId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
