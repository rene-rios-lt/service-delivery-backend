using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Vehicles.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Vehicles;

public class GetAvailableVehiclesQueryHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepositoryMock;
    private readonly Mock<IRepStateRepository> _repStateRepositoryMock;
    private readonly GetAvailableVehiclesQueryHandler _handler;

    public GetAvailableVehiclesQueryHandlerTests()
    {
        _vehicleRepositoryMock = new Mock<IVehicleRepository>();
        _repStateRepositoryMock = new Mock<IRepStateRepository>();
        _handler = new GetAvailableVehiclesQueryHandler(
            _vehicleRepositoryMock.Object, _repStateRepositoryMock.Object);
    }

    private void SetupVehicles(Guid dealerId, params Vehicle[] vehicles) =>
        _vehicleRepositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicles.ToList());

    private void SetupRepState(Guid repId, RepState state) =>
        _repStateRepositoryMock
            .Setup(r => r.GetByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepStateRecord { RepId = repId, State = state });

    private static Vehicle Vehicle(Guid dealerId, Guid? claimedBy = null, string reg = "V-001") =>
        new()
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            Registration = reg,
            ClaimedByRepId = claimedBy,
            Equipment = new List<VehicleEquipment>()
        };

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenGetAvailableVehiclesHandled_ThenItIsReturned()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var unclaimed = Vehicle(dealerId, claimedBy: null);
        SetupVehicles(dealerId, unclaimed);

        // Act
        var result = await _handler.Handle(new GetAvailableVehiclesQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().ContainSingle(v => v.VehicleId == unclaimed.Id);
    }

    [Fact]
    public async Task GivenAVehicleClaimedByAnIdleRep_WhenGetAvailableVehiclesHandled_ThenItIsReturned()
    {
        // Arrange — the simulator claims all vehicles; an idle (Available) rep can be superseded (FE-007)
        var dealerId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var claimed = Vehicle(dealerId, claimedBy: repId);
        SetupVehicles(dealerId, claimed);
        SetupRepState(repId, RepState.Available);

        // Act
        var result = await _handler.Handle(new GetAvailableVehiclesQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().ContainSingle(v => v.VehicleId == claimed.Id);
    }

    [Theory]
    [InlineData(RepState.EnRoute)]
    [InlineData(RepState.Within15Miles)]
    [InlineData(RepState.OnSite)]
    public async Task GivenAVehicleClaimedByARepOnAnActiveJob_WhenGetAvailableVehiclesHandled_ThenItIsExcluded(
        RepState activeState)
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var claimed = Vehicle(dealerId, claimedBy: repId);
        SetupVehicles(dealerId, claimed);
        SetupRepState(repId, activeState);

        // Act
        var result = await _handler.Handle(new GetAvailableVehiclesQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenAMixOfIdleAndOnJobVehicles_WhenGetAvailableVehiclesHandled_ThenOnlyIdleAreReturned()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var idleRep = Guid.NewGuid();
        var busyRep = Guid.NewGuid();
        var idleVehicle = Vehicle(dealerId, claimedBy: idleRep, reg: "V-IDLE");
        var busyVehicle = Vehicle(dealerId, claimedBy: busyRep, reg: "V-BUSY");
        var unclaimedVehicle = Vehicle(dealerId, claimedBy: null, reg: "V-FREE");
        SetupVehicles(dealerId, idleVehicle, busyVehicle, unclaimedVehicle);
        SetupRepState(idleRep, RepState.Available);
        SetupRepState(busyRep, RepState.EnRoute);

        // Act
        var result = await _handler.Handle(new GetAvailableVehiclesQuery(dealerId), CancellationToken.None);

        // Assert
        result.Select(v => v.VehicleId).Should().BeEquivalentTo(new[] { idleVehicle.Id, unclaimedVehicle.Id });
    }

    [Fact]
    public async Task GivenAnAvailableVehicleWithModel_WhenHandled_ThenDtoIncludesModel()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            Registration = "V-001",
            Model = "Transit 350",
            ClaimedByRepId = null,
            Equipment = new List<VehicleEquipment>()
        };
        SetupVehicles(dealerId, vehicle);

        // Act
        var result = await _handler.Handle(new GetAvailableVehiclesQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().ContainSingle(v => v.Model == "Transit 350");
    }

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenGetAvailableVehiclesHandled_ThenDtoContainsIdRegistrationAndEquipment()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            DealerId = dealerId,
            Registration = "V-007",
            ClaimedByRepId = null,
            Equipment = new List<VehicleEquipment>
            {
                new() { VehicleId = vehicleId, EquipmentType = EquipmentType.HydraulicTool },
                new() { VehicleId = vehicleId, EquipmentType = EquipmentType.ElectricalDiagnosticKit }
            }
        };
        SetupVehicles(dealerId, vehicle);

        // Act
        var result = await _handler.Handle(new GetAvailableVehiclesQuery(dealerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.VehicleId.Should().Be(vehicleId);
        dto.Registration.Should().Be("V-007");
        dto.Equipment.Should().BeEquivalentTo(new[] { "HydraulicTool", "ElectricalDiagnosticKit" });
    }
}
