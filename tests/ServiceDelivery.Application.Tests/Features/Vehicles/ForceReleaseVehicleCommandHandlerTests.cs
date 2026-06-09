using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

public class ForceReleaseVehicleCommandHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock;
    private readonly Mock<IRepSessionRepository> _repSessionRepoMock;
    private readonly Mock<IRepStateRepository> _repStateRepoMock;
    private readonly Mock<IRepHubService> _repHubServiceMock;
    private readonly ForceReleaseVehicleCommandHandler _handler;

    public ForceReleaseVehicleCommandHandlerTests()
    {
        _vehicleRepoMock = new Mock<IVehicleRepository>();
        _repSessionRepoMock = new Mock<IRepSessionRepository>();
        _repStateRepoMock = new Mock<IRepStateRepository>();
        _repHubServiceMock = new Mock<IRepHubService>();
        _handler = new ForceReleaseVehicleCommandHandler(
            _vehicleRepoMock.Object,
            _repSessionRepoMock.Object,
            _repStateRepoMock.Object,
            _repHubServiceMock.Object);
    }

    private void SetupVehicle(Guid vehicleId, Guid repId, Vehicle vehicle)
    {
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
    }

    private void SetupRepSession(Guid repId, RepSession? session)
    {
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupRepState(Guid repId, RepStateRecord? repState)
    {
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
    }

    // AC-1: Vehicle transitions Claimed → Unclaimed (ClaimedByRepId cleared)
    [Fact]
    public async Task GivenAVehicleClaimedByRep_WhenForceReleaseCommandHandled_ThenVehicleClaimedByRepIdIsNull()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, Registration = "ABC-001", ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };

        SetupVehicle(vehicleId, repId, vehicle);
        SetupRepSession(repId, session);
        SetupRepState(repId, repState);

        var command = new ForceReleaseVehicleCommand(vehicleId, dispatcherId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedByRepId.Should().BeNull();
    }

    // AC-1: Vehicle transitions Claimed → Unclaimed (ClaimedAt cleared)
    [Fact]
    public async Task GivenAVehicleClaimedByRep_WhenForceReleaseCommandHandled_ThenVehicleClaimedAtIsNull()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, Registration = "ABC-001", ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };

        SetupVehicle(vehicleId, repId, vehicle);
        SetupRepSession(repId, session);
        SetupRepState(repId, repState);

        var command = new ForceReleaseVehicleCommand(vehicleId, dispatcherId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedAt.Should().BeNull();
    }

    // AC-1: Force-release works when the vehicle is claimed by a rep other than the dispatcher
    [Fact]
    public async Task GivenAVehicleClaimedByADifferentRep_WhenForceReleaseCommandHandled_ThenVehicleIsUnclaimed()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, Registration = "ABC-001", ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };

        SetupVehicle(vehicleId, repId, vehicle);
        SetupRepSession(repId, session);
        SetupRepState(repId, repState);

        var command = new ForceReleaseVehicleCommand(vehicleId, dispatcherId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedByRepId.Should().BeNull();
        vehicle.ClaimedAt.Should().BeNull();
    }

    // AC-2: Affected rep's session closed (EndedAt set)
    [Fact]
    public async Task GivenAVehicleClaimedByRep_WhenForceReleaseCommandHandled_ThenRepSessionEndedAtIsSet()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, Registration = "ABC-001", ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };
        var before = DateTime.UtcNow;

        SetupVehicle(vehicleId, repId, vehicle);
        SetupRepSession(repId, session);
        SetupRepState(repId, repState);

        var command = new ForceReleaseVehicleCommand(vehicleId, dispatcherId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        session.EndedAt.Should().NotBeNull();
        session.EndedAt!.Value.Should().BeOnOrAfter(before);
    }

    // AC-2: Affected rep's state transitions to Offline
    [Fact]
    public async Task GivenAVehicleClaimedByRep_WhenForceReleaseCommandHandled_ThenRepStateTransitionsToOffline()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, Registration = "ABC-001", ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };
        RepStateRecord? capturedState = null;

        SetupVehicle(vehicleId, repId, vehicle);
        SetupRepSession(repId, session);
        SetupRepState(repId, repState);
        _repStateRepoMock.Setup(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()))
            .Callback<RepStateRecord, CancellationToken>((s, _) => capturedState = s)
            .Returns(Task.CompletedTask);

        var command = new ForceReleaseVehicleCommand(vehicleId, dispatcherId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.State.Should().Be(RepState.Offline);
    }

    // AC-2: No session update attempted if vehicle has no active rep session
    [Fact]
    public async Task GivenAClaimedVehicleWithNoActiveSession_WhenForceReleaseCommandHandled_ThenNoSessionUpdateAttempted()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, Registration = "ABC-001", ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };

        SetupVehicle(vehicleId, repId, vehicle);
        SetupRepSession(repId, null);
        SetupRepState(repId, repState);

        var command = new ForceReleaseVehicleCommand(vehicleId, dispatcherId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repSessionRepoMock.Verify(r => r.UpdateAsync(It.IsAny<RepSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // AC-4: Notification is sent to the correct rep's user group only (not all reps)
    [Fact]
    public async Task GivenAVehicleClaimedByRep_WhenForceReleaseCommandHandled_ThenRepHubServiceCalledWithCorrectRepGroup()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, Registration = "XYZ-999", ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };
        string? capturedGroup = null;
        VehicleForceReleasedPayload? capturedPayload = null;

        SetupVehicle(vehicleId, repId, vehicle);
        SetupRepSession(repId, session);
        SetupRepState(repId, repState);
        _repHubServiceMock.Setup(h => h.SendVehicleForceReleasedAsync(
                It.IsAny<string>(), It.IsAny<VehicleForceReleasedPayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, VehicleForceReleasedPayload, CancellationToken>((g, p, _) =>
            {
                capturedGroup = g;
                capturedPayload = p;
            })
            .Returns(Task.CompletedTask);

        var command = new ForceReleaseVehicleCommand(vehicleId, dispatcherId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedGroup.Should().Be($"rep:{repId}");
        capturedPayload.Should().NotBeNull();
        capturedPayload!.VehicleId.Should().Be(vehicleId);
        capturedPayload.Registration.Should().Be("XYZ-999");
    }

    // AC-4: Force-release succeeds even when rep has no active hub connection (no state record)
    [Fact]
    public async Task GivenAVehicleClaimedByRepWithNoState_WhenForceReleaseCommandHandled_ThenVehicleIsUnclaimedAndNoHubCallMade()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, Registration = "ABC-001", ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };

        SetupVehicle(vehicleId, repId, vehicle);
        SetupRepSession(repId, null);
        SetupRepState(repId, null);

        var command = new ForceReleaseVehicleCommand(vehicleId, dispatcherId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedByRepId.Should().BeNull();
        vehicle.ClaimedAt.Should().BeNull();
        _repHubServiceMock.Verify(h => h.SendVehicleForceReleasedAsync(
            It.IsAny<string>(), It.IsAny<VehicleForceReleasedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
