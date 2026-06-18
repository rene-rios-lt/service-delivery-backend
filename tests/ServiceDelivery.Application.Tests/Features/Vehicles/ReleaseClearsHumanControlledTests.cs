using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

// AC-3: an explicit release/logout by a human-controlled rep must clear HumanControlled — the handler
// previously set State = Offline directly, leaving the flag set. This guards the GoOffline() switch.
public class ReleaseClearsHumanControlledTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock = new();
    private readonly Mock<IRepSessionRepository> _repSessionRepoMock = new();
    private readonly Mock<IRepStateRepository> _repStateRepoMock = new();
    private readonly ReleaseVehicleCommandHandler _handler;

    public ReleaseClearsHumanControlledTests()
    {
        _handler = new ReleaseVehicleCommandHandler(
            _vehicleRepoMock.Object,
            _repSessionRepoMock.Object,
            _repStateRepoMock.Object);
    }

    [Fact]
    public async Task GivenAHumanControlledRep_WhenVehicleReleased_ThenHumanControlledIsCleared()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available, HumanControlled = true };

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        RepStateRecord? captured = null;
        _repStateRepoMock.Setup(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()))
            .Callback<RepStateRecord, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(new ReleaseVehicleCommand(vehicleId, repId), CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.HumanControlled.Should().BeFalse();
        captured.State.Should().Be(RepState.Offline);
    }
}
