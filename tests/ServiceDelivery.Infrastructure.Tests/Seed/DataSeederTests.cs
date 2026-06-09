using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Infrastructure.Tests.Seed;

public class DataSeederTests
{
    private static AppDbContext CreateFreshContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // AC-1
    [Fact]
    public async Task GivenEmptyDatabase_WhenSeeded_ThenTenDtcsExist()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        context.DiagnosticTroubleCodes.Count().Should().Be(10);
    }

    // AC-1b
    [Fact]
    public async Task GivenEmptyDatabase_WhenSeeded_ThenEachDtcHasCorrectRequiredEquipment()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        var dtcs = context.DiagnosticTroubleCodes.OrderBy(d => d.Code).ToList();
        dtcs.Should().Contain(d => d.Code == "DTC-001" && d.RequiredEquipmentType == EquipmentType.HydraulicTool);
        dtcs.Should().Contain(d => d.Code == "DTC-002" && d.RequiredEquipmentType == EquipmentType.ElectricalDiagnosticKit);
        dtcs.Should().Contain(d => d.Code == "DTC-003" && d.RequiredEquipmentType == EquipmentType.TransmissionKit);
        dtcs.Should().Contain(d => d.Code == "DTC-004" && d.RequiredEquipmentType == EquipmentType.BrakingSystemKit);
        dtcs.Should().Contain(d => d.Code == "DTC-005" && d.RequiredEquipmentType == EquipmentType.CoolingSystemKit);
        dtcs.Should().Contain(d => d.Code == "DTC-006" && d.RequiredEquipmentType == EquipmentType.FuelSystemKit);
        dtcs.Should().Contain(d => d.Code == "DTC-007" && d.RequiredEquipmentType == EquipmentType.ExhaustSystemKit);
        dtcs.Should().Contain(d => d.Code == "DTC-008" && d.RequiredEquipmentType == EquipmentType.SuspensionKit);
        dtcs.Should().Contain(d => d.Code == "DTC-009" && d.RequiredEquipmentType == EquipmentType.SteeringKit);
        dtcs.Should().Contain(d => d.Code == "DTC-010" && d.RequiredEquipmentType == EquipmentType.PowertrainKit);
    }

    // AC-2
    [Fact]
    public async Task GivenEmptyDatabase_WhenSeeded_ThenEightVehiclesExist()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        context.Vehicles.Count().Should().Be(8);
    }

    // AC-2b
    [Fact]
    public async Task GivenEmptyDatabase_WhenSeeded_ThenEachVehicleHasSixEquipmentTypes()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        var vehicles = context.Vehicles.Include(v => v.Equipment).ToList();
        vehicles.Should().AllSatisfy(v => v.Equipment.Count.Should().Be(6));
    }

    // AC-2c: Equipment coverage per the authoritative vehicle table in domain-model.md.
    // Common DTCs (001/002) covered by 7 vehicles; DTC-004 by 6; DTC-005 by 7.
    // Specialized DTCs covered by 2–4 vehicles — every DTC has at least 2 vehicles.
    [Fact]
    public async Task GivenSeededDatabase_WhenQueryingEquipmentCoverage_ThenCommonDtcsHaveHigherCoverage()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert — counts derived from the vehicle table in domain-model.md
        // V1-V7 carry Hydraulic = 7; V1-V7 carry Electrical = 7
        var hydraulicCount    = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.HydraulicTool);
        var electricalCount   = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.ElectricalDiagnosticKit);
        // V1,V2,V3,V4,V6,V8 carry Braking = 6; V1-V5,V7,V8 carry Cooling = 7
        var brakingCount      = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.BrakingSystemKit);
        var coolingCount      = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.CoolingSystemKit);

        // Specialized: Transmission (V1,V4,V5)=3; Fuel (V1,V5,V7,V8)=4; Exhaust (V2,V6,V8)=3;
        // Suspension (V3,V6,V7,V8)=4; Steering (V3,V5,V7,V8)=4; Powertrain (V2,V4,V6)=3
        var transmissionCount = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.TransmissionKit);
        var fuelCount         = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.FuelSystemKit);
        var exhaustCount      = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.ExhaustSystemKit);
        var suspensionCount   = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.SuspensionKit);
        var steeringCount     = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.SteeringKit);
        var powertrainCount   = context.VehicleEquipment.Count(ve => ve.EquipmentType == EquipmentType.PowertrainKit);

        // Common: high coverage (6–7 vehicles)
        hydraulicCount.Should().BeGreaterThanOrEqualTo(6);
        electricalCount.Should().BeGreaterThanOrEqualTo(6);
        brakingCount.Should().BeGreaterThanOrEqualTo(6);
        coolingCount.Should().BeGreaterThanOrEqualTo(6);

        // Specialized: at least 2 vehicles — no DTC left uncoverable
        transmissionCount.Should().BeGreaterThanOrEqualTo(2);
        fuelCount.Should().BeGreaterThanOrEqualTo(2);
        exhaustCount.Should().BeGreaterThanOrEqualTo(2);
        suspensionCount.Should().BeGreaterThanOrEqualTo(2);
        steeringCount.Should().BeGreaterThanOrEqualTo(2);
        powertrainCount.Should().BeGreaterThanOrEqualTo(2);

        // Specialized must be fewer than common
        transmissionCount.Should().BeLessThan(hydraulicCount);
        fuelCount.Should().BeLessThan(hydraulicCount);
        exhaustCount.Should().BeLessThan(brakingCount);
        powertrainCount.Should().BeLessThan(hydraulicCount);
    }

    // AC-3
    [Fact]
    public async Task GivenEmptyDatabase_WhenSeeded_ThenTwoDispatchersExist()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        context.Users.Count(u => u.Role == UserRole.Dispatcher).Should().Be(2);
    }

    // AC-4
    [Fact]
    public async Task GivenEmptyDatabase_WhenSeeded_ThenEightServiceRepsExist()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        context.Users.Count(u => u.Role == UserRole.ServiceRep).Should().Be(8);
    }

    // AC-5
    [Fact]
    public async Task GivenEmptyDatabase_WhenSeeded_ThenTenRequestersExistWithCorrectTierDistribution()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        context.Users.Count(u => u.Role == UserRole.Requester).Should().Be(10);
    }

    // AC-5b
    [Fact]
    public async Task GivenSeededDatabase_WhenQueryingRequesters_ThenTierCountsAreSixThreeOne()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        var requesters = context.Users.Where(u => u.Role == UserRole.Requester).ToList();
        requesters.Count(u => u.Tier == ServiceTier.Bronze).Should().Be(6);
        requesters.Count(u => u.Tier == ServiceTier.Silver).Should().Be(3);
        requesters.Count(u => u.Tier == ServiceTier.Gold).Should().Be(1);
    }

    // AC-6
    [Fact]
    public async Task GivenEmptyDatabase_WhenSeeded_ThenOneSimulatorAccountExists()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        context.Users.Count(u => u.Role == UserRole.Simulator).Should().Be(1);
    }

    // AC-7
    [Fact]
    public async Task GivenAlreadySeededDatabase_WhenSeededAgain_ThenRecordCountsRemainUnchanged()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);
        await seeder.SeedAsync();

        var dtcCountBefore     = context.DiagnosticTroubleCodes.Count();
        var vehicleCountBefore = context.Vehicles.Count();
        var userCountBefore    = context.Users.Count();

        // Act
        await seeder.SeedAsync();

        // Assert
        context.DiagnosticTroubleCodes.Count().Should().Be(dtcCountBefore);
        context.Vehicles.Count().Should().Be(vehicleCountBefore);
        context.Users.Count().Should().Be(userCountBefore);
    }

    // AC-7b
    [Fact]
    public async Task GivenAlreadySeededDatabase_WhenSeededAgain_ThenDtcCountIsStillTen()
    {
        // Arrange
        await using var context = CreateFreshContext();
        var seeder = new DataSeeder(context);
        await seeder.SeedAsync();

        // Act
        await seeder.SeedAsync();

        // Assert
        context.DiagnosticTroubleCodes.Count().Should().Be(10);
    }
}
