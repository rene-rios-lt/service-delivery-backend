using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

public class UpdateVehiclePositionCommandHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock;
    private readonly Mock<IRepStateRepository> _repStateRepoMock;
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepoMock;
    private readonly Mock<IVehiclePositionHubService> _vehiclePositionHubMock;
    private readonly Mock<IRequesterHubService> _requesterHubMock;
    private readonly UpdateVehiclePositionCommandHandler _handler;

    public UpdateVehiclePositionCommandHandlerTests()
    {
        _vehicleRepoMock = new Mock<IVehicleRepository>();
        _repStateRepoMock = new Mock<IRepStateRepository>();
        _serviceRequestRepoMock = new Mock<IServiceRequestRepository>();
        _vehiclePositionHubMock = new Mock<IVehiclePositionHubService>();
        _requesterHubMock = new Mock<IRequesterHubService>();
        _handler = new UpdateVehiclePositionCommandHandler(
            _vehicleRepoMock.Object,
            _repStateRepoMock.Object,
            _serviceRequestRepoMock.Object,
            _vehiclePositionHubMock.Object,
            _requesterHubMock.Object);
    }

    private Vehicle BuildVehicle(Guid vehicleId, Guid repId, Guid dealerId)
        => new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            ClaimedByRepId = repId
        };

    private RepStateRecord BuildRepState(Guid repId, RepState state)
        => new RepStateRecord { RepId = repId, State = state };

    private ServiceRequest BuildServiceRequest(Guid repId, Guid requesterId, ServiceRequestStatus status)
        => new ServiceRequest
        {
            Id = Guid.NewGuid(),
            DealerId = Guid.NewGuid(),
            RequesterId = requesterId,
            AssignedRepId = repId,
            Latitude = 40.7128,
            Longitude = -74.0060,
            Status = status
        };

    // AC-1: Persists { lat, lng, timestamp } against the vehicle
    [Fact]
    public async Task GivenAValidPositionUpdate_WhenHandled_ThenVehicleLastPositionIsPersisted()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var vehicle = BuildVehicle(vehicleId, repId, dealerId);
        var repState = BuildRepState(repId, RepState.Available);
        var timestamp = DateTime.UtcNow;

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        _serviceRequestRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var command = new UpdateVehiclePositionCommand(vehicleId, Guid.NewGuid(), 51.5074, -0.1278, timestamp);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        vehicle.LastLatitude.Should().Be(51.5074);
        vehicle.LastLongitude.Should().Be(-0.1278);
        vehicle.LastPositionUpdatedAt.Should().Be(timestamp);
        _vehicleRepoMock.Verify(r => r.UpdateAsync(vehicle, It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-2: Rep NOT EnRoute → no Haversine recalculation or state transition
    [Fact]
    public async Task GivenARepNotEnRoute_WhenPositionUpdated_ThenNoStateTransitionOccurs()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var vehicle = BuildVehicle(vehicleId, repId, dealerId);
        var repState = BuildRepState(repId, RepState.Available);

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        _serviceRequestRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var command = new UpdateVehiclePositionCommand(vehicleId, Guid.NewGuid(), 51.5074, -0.1278, DateTime.UtcNow);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repStateRepoMock.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // AC-2: Rep EnRoute with Assigned request → Haversine recalculation occurs
    [Fact]
    public async Task GivenAnEnRouteRepWithAssignedRequest_WhenPositionUpdated_ThenHaversineIsRecalculated()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var vehicle = BuildVehicle(vehicleId, repId, dealerId);
        var repState = BuildRepState(repId, RepState.EnRoute);
        var serviceRequest = BuildServiceRequest(repId, requesterId, ServiceRequestStatus.Assigned);

        // Place request far away — more than 15 miles
        serviceRequest.Latitude = 51.5074;
        serviceRequest.Longitude = 0.1276; // ~11km east

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        _serviceRequestRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceRequest);

        // Vehicle is more than 15 miles from request
        var command = new UpdateVehiclePositionCommand(vehicleId, Guid.NewGuid(), 51.5074, -3.1884, DateTime.UtcNow);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — state should have been evaluated (upserted since rep stays EnRoute beyond 15 miles)
        _repStateRepoMock.Verify(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-3: Distance < 15 miles → rep state transitions to Within15Miles
    [Fact]
    public async Task GivenAnEnRouteRepWithinFifteenMiles_WhenPositionUpdated_ThenRepStateTransitionsToWithin15Miles()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var vehicle = BuildVehicle(vehicleId, repId, dealerId);
        var repState = BuildRepState(repId, RepState.EnRoute);
        var serviceRequest = BuildServiceRequest(repId, requesterId, ServiceRequestStatus.Assigned);

        // Request is very close — same coordinates as vehicle position
        serviceRequest.Latitude = 51.5074;
        serviceRequest.Longitude = -0.1278;

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        _serviceRequestRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceRequest);

        RepStateRecord? capturedState = null;
        _repStateRepoMock.Setup(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()))
            .Callback<RepStateRecord, CancellationToken>((s, _) => capturedState = s)
            .Returns(Task.CompletedTask);

        var command = new UpdateVehiclePositionCommand(vehicleId, Guid.NewGuid(), 51.5074, -0.1278, DateTime.UtcNow);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.State.Should().Be(RepState.Within15Miles);
    }

    // AC-3: Distance >= 15 miles → rep state stays EnRoute
    [Fact]
    public async Task GivenAnEnRouteRepBeyondFifteenMiles_WhenPositionUpdated_ThenRepStateRemainsEnRoute()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var vehicle = BuildVehicle(vehicleId, repId, dealerId);
        var repState = BuildRepState(repId, RepState.EnRoute);
        var serviceRequest = BuildServiceRequest(repId, requesterId, ServiceRequestStatus.Assigned);

        // Request at London centre
        serviceRequest.Latitude = 51.5074;
        serviceRequest.Longitude = -0.1278;

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        _serviceRequestRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceRequest);

        RepStateRecord? capturedState = null;
        _repStateRepoMock.Setup(r => r.UpsertAsync(It.IsAny<RepStateRecord>(), It.IsAny<CancellationToken>()))
            .Callback<RepStateRecord, CancellationToken>((s, _) => capturedState = s)
            .Returns(Task.CompletedTask);

        // Vehicle far away — Cardiff (approx 140 miles from London)
        var command = new UpdateVehiclePositionCommand(vehicleId, Guid.NewGuid(), 51.4816, -3.1791, DateTime.UtcNow);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.State.Should().Be(RepState.EnRoute);
    }

    // AC-4: ETA included in RepPositionUpdated payload
    [Fact]
    public async Task GivenAnEnRouteRepWithAssignedRequest_WhenPositionUpdated_ThenRepPositionUpdatedPayloadIncludesEta()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var vehicle = BuildVehicle(vehicleId, repId, dealerId);
        var repState = BuildRepState(repId, RepState.EnRoute);
        var serviceRequest = BuildServiceRequest(repId, requesterId, ServiceRequestStatus.Assigned);

        // Same location — 0 distance, 0 ETA
        serviceRequest.Latitude = 51.5074;
        serviceRequest.Longitude = -0.1278;

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        _repStateRepoMock.Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repState);
        _serviceRequestRepoMock.Setup(r => r.GetActiveByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceRequest);

        RepPositionUpdatedPayload? capturedPayload = null;
        _requesterHubMock.Setup(h => h.SendRepPositionUpdatedAsync(
                It.IsAny<string>(), It.IsAny<RepPositionUpdatedPayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, RepPositionUpdatedPayload, CancellationToken>((_, p, _) => capturedPayload = p)
            .Returns(Task.CompletedTask);

        var command = new UpdateVehiclePositionCommand(vehicleId, Guid.NewGuid(), 51.5074, -0.1278, DateTime.UtcNow);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedPayload.Should().NotBeNull();
        capturedPayload!.EtaMinutes.Should().BeApproximately(0.0, precision: 0.01);
    }
}
