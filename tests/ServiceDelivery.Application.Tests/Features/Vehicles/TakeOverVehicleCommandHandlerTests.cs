using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

public class TakeOverVehicleCommandHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock;
    private readonly Mock<IRepSessionRepository> _repSessionRepoMock;
    private readonly Mock<IRepStateRepository> _repStateRepoMock;
    private readonly Mock<IDispatchHubService> _dispatchHubMock;
    private readonly TakeOverVehicleCommandHandler _handler;

    public TakeOverVehicleCommandHandlerTests()
    {
        _vehicleRepoMock = new Mock<IVehicleRepository>();
        _repSessionRepoMock = new Mock<IRepSessionRepository>();
        _repStateRepoMock = new Mock<IRepStateRepository>();
        _dispatchHubMock = new Mock<IDispatchHubService>();
        _handler = new TakeOverVehicleCommandHandler(
            _vehicleRepoMock.Object,
            _repSessionRepoMock.Object,
            _repStateRepoMock.Object,
            _dispatchHubMock.Object);
    }

    private void SetupVehicle(Guid vehicleId, Vehicle vehicle)
    {
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
    }

    private void SetupRepState(Guid repId, RepStateRecord? repState)
    {
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
    }

    private void SetupRepSession(Guid repId, RepSession? session)
    {
        _repSessionRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    // AC-1: caller rep must be idle — a rep with an active Assigned request is not idle → 409
    [Fact]
    public async Task GivenARepWithAnActiveAssignedRequest_WhenTakeOverCalled_ThenThrowsRepNotIdle()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            DealerId = Guid.NewGuid(),
            ClaimedByRepId = displacedRepId,
            ClaimedAt = DateTime.UtcNow
        };
        SetupVehicle(vehicleId, vehicle);
        SetupRepState(displacedRepId, new RepStateRecord { RepId = displacedRepId, State = RepState.Available });
        SetupRepState(callerRepId, new RepStateRecord
        {
            RepId = callerRepId,
            State = RepState.EnRoute,
            ActiveRequestId = Guid.NewGuid()
        });

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<RepNotIdleException>();
    }

    // AC-1: target vehicle must be idle — its current rep is EnRoute (busy) → 409
    [Fact]
    public async Task GivenAVehicleWhoseRepIsEnRoute_WhenTakeOverCalled_ThenThrowsVehicleNotIdle()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            DealerId = Guid.NewGuid(),
            ClaimedByRepId = displacedRepId,
            ClaimedAt = DateTime.UtcNow
        };
        SetupVehicle(vehicleId, vehicle);
        SetupRepState(callerRepId, new RepStateRecord { RepId = callerRepId, State = RepState.Available });
        SetupRepState(displacedRepId, new RepStateRecord
        {
            RepId = displacedRepId,
            State = RepState.EnRoute,
            ActiveRequestId = Guid.NewGuid()
        });

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<VehicleNotIdleException>();
    }

    private static Vehicle BuildIdleClaimedVehicle(Guid vehicleId, Guid displacedRepId, Guid dealerId)
    {
        return new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-TEST",
            ClaimedByRepId = displacedRepId,
            ClaimedAt = DateTime.UtcNow,
            LastLatitude = 41.6,
            LastLongitude = -93.6
        };
    }

    // AC-2: on success the displaced rep's session is ended and that rep set Offline
    [Fact]
    public async Task GivenAnIdleVehicleClaimedByAnotherRep_WhenTakenOver_ThenPriorRepSessionEndedAndRepOffline()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var vehicle = BuildIdleClaimedVehicle(vehicleId, displacedRepId, Guid.NewGuid());
        var displacedSession = new RepSession { Id = Guid.NewGuid(), RepId = displacedRepId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow };
        var displacedState = new RepStateRecord { RepId = displacedRepId, State = RepState.Available };

        SetupVehicle(vehicleId, vehicle);
        SetupRepState(callerRepId, new RepStateRecord { RepId = callerRepId, State = RepState.Offline });
        SetupRepState(displacedRepId, displacedState);
        SetupRepSession(displacedRepId, displacedSession);
        SetupRepSession(callerRepId, null);

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        displacedSession.EndedAt.Should().NotBeNull();
        displacedState.State.Should().Be(RepState.Offline);
    }

    // AC-2: the caller's own prior session (if any) is ended
    [Fact]
    public async Task GivenACallerWithAnExistingSession_WhenTakeOverSucceeds_ThenCallerPriorSessionEnded()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var vehicle = BuildIdleClaimedVehicle(vehicleId, displacedRepId, Guid.NewGuid());
        var callerPriorSession = new RepSession { Id = Guid.NewGuid(), RepId = callerRepId, VehicleId = Guid.NewGuid(), StartedAt = DateTime.UtcNow };

        SetupVehicle(vehicleId, vehicle);
        SetupRepState(callerRepId, new RepStateRecord { RepId = callerRepId, State = RepState.Available });
        SetupRepState(displacedRepId, new RepStateRecord { RepId = displacedRepId, State = RepState.Available });
        SetupRepSession(displacedRepId, new RepSession { Id = Guid.NewGuid(), RepId = displacedRepId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow });
        SetupRepSession(callerRepId, callerPriorSession);

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        callerPriorSession.EndedAt.Should().NotBeNull();
    }

    // AC-2: a new RepSession is created for the caller on the target vehicle
    [Fact]
    public async Task GivenAnIdleRepAndIdleVehicle_WhenTakenOver_ThenNewSessionCreatedForCallerOnVehicle()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var vehicle = BuildIdleClaimedVehicle(vehicleId, displacedRepId, Guid.NewGuid());
        RepSession? addedSession = null;

        SetupVehicle(vehicleId, vehicle);
        SetupRepState(callerRepId, new RepStateRecord { RepId = callerRepId, State = RepState.Offline });
        SetupRepState(displacedRepId, new RepStateRecord { RepId = displacedRepId, State = RepState.Available });
        SetupRepSession(displacedRepId, new RepSession { Id = Guid.NewGuid(), RepId = displacedRepId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow });
        SetupRepSession(callerRepId, null);
        _repSessionRepoMock.Setup(r => r.AddAsync(It.IsAny<RepSession>(), It.IsAny<CancellationToken>()))
            .Callback<RepSession, CancellationToken>((s, _) => addedSession = s)
            .Returns(Task.CompletedTask);

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        addedSession.Should().NotBeNull();
        addedSession!.RepId.Should().Be(callerRepId);
        addedSession.VehicleId.Should().Be(vehicleId);
        addedSession.EndedAt.Should().BeNull();
    }

    // AC-2: caller set Available, HumanControlled, with LastHeartbeatAt stamped
    [Fact]
    public async Task GivenAnIdleRep_WhenTakenOver_ThenRepIsAvailableHumanControlledWithHeartbeatStamped()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var vehicle = BuildIdleClaimedVehicle(vehicleId, displacedRepId, Guid.NewGuid());
        var callerState = new RepStateRecord { RepId = callerRepId, State = RepState.Offline };
        var before = DateTime.UtcNow;

        SetupVehicle(vehicleId, vehicle);
        SetupRepState(callerRepId, callerState);
        SetupRepState(displacedRepId, new RepStateRecord { RepId = displacedRepId, State = RepState.Available });
        SetupRepSession(displacedRepId, new RepSession { Id = Guid.NewGuid(), RepId = displacedRepId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow });
        SetupRepSession(callerRepId, null);

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        callerState.State.Should().Be(RepState.Available);
        callerState.HumanControlled.Should().BeTrue();
        callerState.LastHeartbeatAt.Should().NotBeNull();
        callerState.LastHeartbeatAt!.Value.Should().BeOnOrAfter(before);
    }

    // AC-2: the vehicle is re-claimed by the caller
    [Fact]
    public async Task GivenAnIdleVehicle_WhenTakenOver_ThenVehicleClaimedByCaller()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var vehicle = BuildIdleClaimedVehicle(vehicleId, displacedRepId, Guid.NewGuid());

        SetupVehicle(vehicleId, vehicle);
        SetupRepState(callerRepId, new RepStateRecord { RepId = callerRepId, State = RepState.Offline });
        SetupRepState(displacedRepId, new RepStateRecord { RepId = displacedRepId, State = RepState.Available });
        SetupRepSession(displacedRepId, new RepSession { Id = Guid.NewGuid(), RepId = displacedRepId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow });
        SetupRepSession(callerRepId, null);

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.ClaimedByRepId.Should().Be(callerRepId);
        vehicle.ClaimedAt.Should().NotBeNull();
        result.RepId.Should().Be(callerRepId);
        result.VehicleId.Should().Be(vehicleId);
        result.RepState.Should().Be("Available");
        result.SessionId.Should().NotBeEmpty();
    }

    // AC-3: RepStateChanged is broadcast to the vehicle's dealer group for the caller (→ Available)
    [Fact]
    public async Task GivenAnIdleRep_WhenTakenOver_ThenRepStateChangedBroadcastToDealerGroup()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var vehicle = BuildIdleClaimedVehicle(vehicleId, displacedRepId, dealerId);
        string? capturedGroup = null;
        RepStateChangedPayload? capturedPayload = null;

        SetupVehicle(vehicleId, vehicle);
        SetupRepState(callerRepId, new RepStateRecord { RepId = callerRepId, State = RepState.Offline });
        SetupRepState(displacedRepId, new RepStateRecord { RepId = displacedRepId, State = RepState.Available });
        SetupRepSession(displacedRepId, new RepSession { Id = Guid.NewGuid(), RepId = displacedRepId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow });
        SetupRepSession(callerRepId, null);
        _dispatchHubMock.Setup(h => h.SendRepStateChangedAsync(
                It.IsAny<string>(),
                It.Is<RepStateChangedPayload>(p => p.RepId == callerRepId),
                It.IsAny<CancellationToken>()))
            .Callback<string, RepStateChangedPayload, CancellationToken>((g, p, _) =>
            {
                capturedGroup = g;
                capturedPayload = p;
            })
            .Returns(Task.CompletedTask);

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedGroup.Should().Be($"dealer:{dealerId}");
        capturedPayload.Should().NotBeNull();
        capturedPayload!.RepId.Should().Be(callerRepId);
        capturedPayload.NewState.Should().Be("Available");
    }

    // AC-3: a fleet position update is broadcast to the dealer group reusing the vehicle position
    [Fact]
    public async Task GivenAnIdleRepWithKnownVehiclePosition_WhenTakenOver_ThenFleetPositionUpdateBroadcastToDealerGroup()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var callerRepId = Guid.NewGuid();
        var displacedRepId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var vehicle = BuildIdleClaimedVehicle(vehicleId, displacedRepId, dealerId);
        string? capturedGroup = null;
        FleetPositionUpdatePayload? capturedPayload = null;

        SetupVehicle(vehicleId, vehicle);
        SetupRepState(callerRepId, new RepStateRecord { RepId = callerRepId, State = RepState.Offline });
        SetupRepState(displacedRepId, new RepStateRecord { RepId = displacedRepId, State = RepState.Available });
        SetupRepSession(displacedRepId, new RepSession { Id = Guid.NewGuid(), RepId = displacedRepId, VehicleId = vehicleId, StartedAt = DateTime.UtcNow });
        SetupRepSession(callerRepId, null);
        _dispatchHubMock.Setup(h => h.SendFleetPositionUpdateAsync(
                It.IsAny<string>(), It.IsAny<FleetPositionUpdatePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, FleetPositionUpdatePayload, CancellationToken>((g, p, _) =>
            {
                capturedGroup = g;
                capturedPayload = p;
            })
            .Returns(Task.CompletedTask);

        var command = new TakeOverVehicleCommand(vehicleId, callerRepId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedGroup.Should().Be($"dealer:{dealerId}");
        capturedPayload.Should().NotBeNull();
        capturedPayload!.RepId.Should().Be(callerRepId);
        capturedPayload.Latitude.Should().Be(41.6);
        capturedPayload.Longitude.Should().Be(-93.6);
        capturedPayload.State.Should().Be("Available");
    }
}
