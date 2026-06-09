using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

public class ClaimVehicleCommandHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock;
    private readonly Mock<IRepSessionRepository> _repSessionRepoMock;
    private readonly Mock<IRepStateRepository> _repStateRepoMock;
    private readonly ClaimVehicleCommandHandler _handler;

    public ClaimVehicleCommandHandlerTests()
    {
        _vehicleRepoMock = new Mock<IVehicleRepository>();
        _repSessionRepoMock = new Mock<IRepSessionRepository>();
        _repStateRepoMock = new Mock<IRepStateRepository>();
        _handler = new ClaimVehicleCommandHandler(
            _vehicleRepoMock.Object,
            _repSessionRepoMock.Object,
            _repStateRepoMock.Object);
    }

    // AC-1: Vehicle transitions Unclaimed → Claimed (sets ClaimedByRepId)
    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenClaimCommandHandled_ThenVehicleClaimedByRepIdIsSet()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = null };

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepSession?)null);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepStateRecord?)null);

        var command = new ClaimVehicleCommand(vehicleId, repId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedByRepId.Should().Be(repId);
    }

    // AC-1: Vehicle transitions Unclaimed → Claimed (sets ClaimedAt)
    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenClaimCommandHandled_ThenVehicleClaimedAtIsSet()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = null };
        var before = DateTime.UtcNow;

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepSession?)null);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepStateRecord?)null);

        var command = new ClaimVehicleCommand(vehicleId, repId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedAt.Should().NotBeNull();
        vehicle.ClaimedAt!.Value.Should().BeOnOrAfter(before);
    }

    // AC-2: Active rep session created linking rep to vehicle
    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenClaimCommandHandled_ThenRepSessionIsCreatedWithCorrectRepIdAndVehicleId()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = null };
        RepSession? capturedSession = null;

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepSession?)null);
        _repSessionRepoMock.Setup(r => r.AddAsync(It.IsAny<RepSession>(), It.IsAny<CancellationToken>()))
            .Callback<RepSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepStateRecord?)null);

        var command = new ClaimVehicleCommand(vehicleId, repId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedSession.Should().NotBeNull();
        capturedSession!.RepId.Should().Be(repId);
        capturedSession.VehicleId.Should().Be(vehicleId);
        capturedSession.EndedAt.Should().BeNull();
    }

    // AC-2: Rep state transitions Offline → Available
    [Fact]
    public async Task GivenAnOfflineRep_WhenVehicleClaimed_ThenRepStateTransitionsToAvailable()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = null };
        RepStateRecord? capturedState = null;

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepSession?)null);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepStateRecord?)null);
        _repStateRepoMock.Setup(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()))
            .Callback<RepStateRecord, CancellationToken>((s, _) => capturedState = s)
            .Returns(Task.CompletedTask);

        var command = new ClaimVehicleCommand(vehicleId, repId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.RepId.Should().Be(repId);
        capturedState.State.Should().Be(RepState.Available);
    }

    // AC-3: Returns conflict if vehicle is already claimed
    [Fact]
    public async Task GivenAnAlreadyClaimedVehicle_WhenClaimCommandHandled_ThenThrowsVehicleAlreadyClaimedException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = Guid.NewGuid() };

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepSession?)null);

        var command = new ClaimVehicleCommand(vehicleId, repId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<VehicleAlreadyClaimedException>();
    }

    // AC-4: Returns conflict if rep already has an active session
    [Fact]
    public async Task GivenARepWithAnActiveSession_WhenClaimCommandHandled_ThenThrowsRepAlreadyHasSessionException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, ClaimedByRepId = null };
        var existingSession = new RepSession { Id = Guid.NewGuid(), RepId = repId, VehicleId = Guid.NewGuid() };

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSession);

        var command = new ClaimVehicleCommand(vehicleId, repId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<RepAlreadyHasActiveSessionException>();
    }

    // Not-found case: throws when vehicle not found
    [Fact]
    public async Task GivenANonExistentVehicleId_WhenClaimCommandHandled_ThenThrowsKeyNotFoundException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vehicle?)null);

        var command = new ClaimVehicleCommand(vehicleId, repId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
