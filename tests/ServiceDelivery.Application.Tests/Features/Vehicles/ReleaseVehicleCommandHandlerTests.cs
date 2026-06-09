using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

public class ReleaseVehicleCommandHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock;
    private readonly Mock<IRepSessionRepository> _repSessionRepoMock;
    private readonly Mock<IRepStateRepository> _repStateRepoMock;
    private readonly ReleaseVehicleCommandHandler _handler;

    public ReleaseVehicleCommandHandlerTests()
    {
        _vehicleRepoMock = new Mock<IVehicleRepository>();
        _repSessionRepoMock = new Mock<IRepSessionRepository>();
        _repStateRepoMock = new Mock<IRepStateRepository>();
        _handler = new ReleaseVehicleCommandHandler(
            _vehicleRepoMock.Object,
            _repSessionRepoMock.Object,
            _repStateRepoMock.Object);
    }

    private void SetupHappyPath(Guid vehicleId, Guid repId, Vehicle vehicle, RepSession session, RepStateRecord repState)
    {
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    // AC-1: Vehicle transitions Claimed → Unclaimed (ClaimedByRepId cleared)
    [Fact]
    public async Task GivenAClaimedVehicle_WhenReleaseCommandHandled_ThenVehicleClaimedByRepIdIsNull()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };
        SetupHappyPath(vehicleId, repId, vehicle, session, repState);

        var command = new ReleaseVehicleCommand(vehicleId, repId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedByRepId.Should().BeNull();
    }

    // AC-1: Vehicle transitions Claimed → Unclaimed (ClaimedAt cleared)
    [Fact]
    public async Task GivenAClaimedVehicle_WhenReleaseCommandHandled_ThenVehicleClaimedAtIsNull()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };
        SetupHappyPath(vehicleId, repId, vehicle, session, repState);

        var command = new ReleaseVehicleCommand(vehicleId, repId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedAt.Should().BeNull();
    }

    // AC-2: Rep session closed (EndedAt set)
    [Fact]
    public async Task GivenAClaimedVehicle_WhenReleaseCommandHandled_ThenRepSessionEndedAtIsSet()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };
        SetupHappyPath(vehicleId, repId, vehicle, session, repState);
        var before = DateTime.UtcNow;

        var command = new ReleaseVehicleCommand(vehicleId, repId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        session.EndedAt.Should().NotBeNull();
        session.EndedAt!.Value.Should().BeOnOrAfter(before);
    }

    // AC-2: Rep state transitions to Offline
    [Fact]
    public async Task GivenAClaimedVehicle_WhenReleaseCommandHandled_ThenRepStateTransitionsToOffline()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = repId, ClaimedAt = DateTime.UtcNow };
        var session = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var repState = new RepStateRecord { RepId = repId, State = RepState.Available };
        RepStateRecord? capturedState = null;
        SetupHappyPath(vehicleId, repId, vehicle, session, repState);
        _repStateRepoMock.Setup(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()))
            .Callback<RepStateRecord, CancellationToken>((s, _) => capturedState = s)
            .Returns(Task.CompletedTask);

        var command = new ReleaseVehicleCommand(vehicleId, repId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.State.Should().Be(RepState.Offline);
    }

    // AC-3: Returns 400 if vehicle is unclaimed (ClaimedByRepId is null)
    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenReleaseCommandHandled_ThenThrowsVehicleNotClaimedByRepException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = null };

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        var command = new ReleaseVehicleCommand(vehicleId, repId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<VehicleNotClaimedByRepException>();
    }

    // AC-3: Returns 400 if vehicle is claimed by a different rep
    [Fact]
    public async Task GivenAVehicleClaimedByAnotherRep_WhenReleaseCommandHandled_ThenThrowsVehicleNotClaimedByRepException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var differentRepId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = differentRepId };

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        var command = new ReleaseVehicleCommand(vehicleId, repId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<VehicleNotClaimedByRepException>();
    }

    // AC-4: Cannot release while rep state is OnSite
    [Fact]
    public async Task GivenARepWithOnSiteState_WhenReleaseCommandHandled_ThenThrowsVehicleReleaseBlockedByActiveJobException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = repId };
        var repState = new RepStateRecord { RepId = repId, State = RepState.OnSite };

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);

        var command = new ReleaseVehicleCommand(vehicleId, repId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<VehicleReleaseBlockedByActiveJobException>();
    }

    // Not-found case: throws when vehicle not found
    [Fact]
    public async Task GivenANonExistentVehicleId_WhenReleaseCommandHandled_ThenThrowsKeyNotFoundException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vehicle?)null);

        var command = new ReleaseVehicleCommand(vehicleId, repId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
