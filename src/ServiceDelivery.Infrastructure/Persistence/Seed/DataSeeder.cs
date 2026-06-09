using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Infrastructure.Persistence.Seed;

public class DataSeeder
{
    private readonly AppDbContext _context;

    public DataSeeder(AppDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        await SeedDtcsAsync();
        await SeedVehiclesAsync();
        await SeedUsersAsync();
        await _context.SaveChangesAsync();
    }

    private async Task SeedDtcsAsync()
    {
        if (_context.DiagnosticTroubleCodes.Any())
            return;

        var dtcs = new[]
        {
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc001Id, DealerId = SeedConstants.DealerId, Code = "DTC-001", HumanReadableTitle = "Hydraulic system fault",      RequiredEquipmentType = EquipmentType.HydraulicTool },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc002Id, DealerId = SeedConstants.DealerId, Code = "DTC-002", HumanReadableTitle = "Electrical system fault",     RequiredEquipmentType = EquipmentType.ElectricalDiagnosticKit },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc003Id, DealerId = SeedConstants.DealerId, Code = "DTC-003", HumanReadableTitle = "Transmission fault",          RequiredEquipmentType = EquipmentType.TransmissionKit },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc004Id, DealerId = SeedConstants.DealerId, Code = "DTC-004", HumanReadableTitle = "Braking system fault",        RequiredEquipmentType = EquipmentType.BrakingSystemKit },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc005Id, DealerId = SeedConstants.DealerId, Code = "DTC-005", HumanReadableTitle = "Cooling system overheating",  RequiredEquipmentType = EquipmentType.CoolingSystemKit },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc006Id, DealerId = SeedConstants.DealerId, Code = "DTC-006", HumanReadableTitle = "Fuel system fault",           RequiredEquipmentType = EquipmentType.FuelSystemKit },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc007Id, DealerId = SeedConstants.DealerId, Code = "DTC-007", HumanReadableTitle = "Exhaust system fault",        RequiredEquipmentType = EquipmentType.ExhaustSystemKit },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc008Id, DealerId = SeedConstants.DealerId, Code = "DTC-008", HumanReadableTitle = "Suspension fault",            RequiredEquipmentType = EquipmentType.SuspensionKit },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc009Id, DealerId = SeedConstants.DealerId, Code = "DTC-009", HumanReadableTitle = "Steering system fault",       RequiredEquipmentType = EquipmentType.SteeringKit },
            new DiagnosticTroubleCode { Id = SeedConstants.Dtc010Id, DealerId = SeedConstants.DealerId, Code = "DTC-010", HumanReadableTitle = "Powertrain fault",            RequiredEquipmentType = EquipmentType.PowertrainKit },
        };

        await _context.DiagnosticTroubleCodes.AddRangeAsync(dtcs);
    }

    private async Task SeedVehiclesAsync()
    {
        if (_context.Vehicles.Any())
            return;

        // Equipment distribution from domain-model.md — each vehicle carries exactly 6 of 10 equipment types.
        // Common DTCs (DTC-001/002/004/005) are covered by 6–7 vehicles; specialized DTCs by 2–3 vehicles.
        var vehicles = new[]
        {
            BuildVehicle(SeedConstants.Vehicle1Id, "V-001", EquipmentType.HydraulicTool, EquipmentType.ElectricalDiagnosticKit, EquipmentType.BrakingSystemKit, EquipmentType.CoolingSystemKit, EquipmentType.TransmissionKit,       EquipmentType.FuelSystemKit),
            BuildVehicle(SeedConstants.Vehicle2Id, "V-002", EquipmentType.HydraulicTool, EquipmentType.ElectricalDiagnosticKit, EquipmentType.BrakingSystemKit, EquipmentType.CoolingSystemKit, EquipmentType.PowertrainKit,          EquipmentType.ExhaustSystemKit),
            BuildVehicle(SeedConstants.Vehicle3Id, "V-003", EquipmentType.HydraulicTool, EquipmentType.ElectricalDiagnosticKit, EquipmentType.BrakingSystemKit, EquipmentType.CoolingSystemKit, EquipmentType.SuspensionKit,          EquipmentType.SteeringKit),
            BuildVehicle(SeedConstants.Vehicle4Id, "V-004", EquipmentType.HydraulicTool, EquipmentType.ElectricalDiagnosticKit, EquipmentType.BrakingSystemKit, EquipmentType.CoolingSystemKit, EquipmentType.TransmissionKit,       EquipmentType.PowertrainKit),
            BuildVehicle(SeedConstants.Vehicle5Id, "V-005", EquipmentType.HydraulicTool, EquipmentType.ElectricalDiagnosticKit, EquipmentType.CoolingSystemKit, EquipmentType.FuelSystemKit,    EquipmentType.TransmissionKit,       EquipmentType.SteeringKit),
            BuildVehicle(SeedConstants.Vehicle6Id, "V-006", EquipmentType.HydraulicTool, EquipmentType.ElectricalDiagnosticKit, EquipmentType.BrakingSystemKit, EquipmentType.ExhaustSystemKit, EquipmentType.SuspensionKit,         EquipmentType.PowertrainKit),
            BuildVehicle(SeedConstants.Vehicle7Id, "V-007", EquipmentType.HydraulicTool, EquipmentType.ElectricalDiagnosticKit, EquipmentType.CoolingSystemKit, EquipmentType.FuelSystemKit,    EquipmentType.SuspensionKit,         EquipmentType.SteeringKit),
            BuildVehicle(SeedConstants.Vehicle8Id, "V-008", EquipmentType.BrakingSystemKit, EquipmentType.CoolingSystemKit,     EquipmentType.FuelSystemKit,    EquipmentType.ExhaustSystemKit, EquipmentType.SuspensionKit,         EquipmentType.SteeringKit),
            BuildDealer2Vehicle(SeedConstants.Dealer2Vehicle1Id, "D2-001", EquipmentType.HydraulicTool, EquipmentType.ElectricalDiagnosticKit),
        };

        await _context.Vehicles.AddRangeAsync(vehicles);
    }

    private async Task SeedUsersAsync()
    {
        if (_context.Users.Any())
            return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(SeedConstants.DefaultPassword);

        var users = new[]
        {
            // Dispatchers
            new User { Id = SeedConstants.AlexDispatcherId,      Name = "Alex Dispatcher",    Email = "alex@dealer.com",              PasswordHash = passwordHash, Role = UserRole.Dispatcher, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.JordanDispatcherId,    Name = "Jordan Dispatcher",  Email = "jordan@dealer.com",            PasswordHash = passwordHash, Role = UserRole.Dispatcher, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Dealer2DispatcherId,   Name = "Dealer2 Dispatcher", Email = "dispatcher@dealer2.com",       PasswordHash = passwordHash, Role = UserRole.Dispatcher, Tier = ServiceTier.None, DealerId = SeedConstants.Dealer2Id },

            // Service Reps
            new User { Id = SeedConstants.Rep1Id, Name = "Rep One",   Email = "rep1@dealer.com", PasswordHash = passwordHash, Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Rep2Id, Name = "Rep Two",   Email = "rep2@dealer.com", PasswordHash = passwordHash, Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Rep3Id, Name = "Rep Three", Email = "rep3@dealer.com", PasswordHash = passwordHash, Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Rep4Id, Name = "Rep Four",  Email = "rep4@dealer.com", PasswordHash = passwordHash, Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Rep5Id, Name = "Rep Five",  Email = "rep5@dealer.com", PasswordHash = passwordHash, Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Rep6Id, Name = "Rep Six",   Email = "rep6@dealer.com", PasswordHash = passwordHash, Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Rep7Id, Name = "Rep Seven", Email = "rep7@dealer.com", PasswordHash = passwordHash, Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Rep8Id, Name = "Rep Eight", Email = "rep8@dealer.com", PasswordHash = passwordHash, Role = UserRole.ServiceRep, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },

            // Requesters — 6 Bronze, 3 Silver, 1 Gold
            new User { Id = SeedConstants.Bronze1Id, Name = "Bronze User 1", Email = "bronze1@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Bronze, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Bronze2Id, Name = "Bronze User 2", Email = "bronze2@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Bronze, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Bronze3Id, Name = "Bronze User 3", Email = "bronze3@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Bronze, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Bronze4Id, Name = "Bronze User 4", Email = "bronze4@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Bronze, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Bronze5Id, Name = "Bronze User 5", Email = "bronze5@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Bronze, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Bronze6Id, Name = "Bronze User 6", Email = "bronze6@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Bronze, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Silver1Id, Name = "Silver User 1", Email = "silver1@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Silver, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Silver2Id, Name = "Silver User 2", Email = "silver2@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Silver, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Silver3Id, Name = "Silver User 3", Email = "silver3@example.com", PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Silver, DealerId = SeedConstants.DealerId },
            new User { Id = SeedConstants.Gold1Id,   Name = "Gold User 1",   Email = "gold1@example.com",   PasswordHash = passwordHash, Role = UserRole.Requester, Tier = ServiceTier.Gold,   DealerId = SeedConstants.DealerId },

            // Simulator
            new User { Id = SeedConstants.SimulatorId, Name = "Simulator", Email = "simulator@system.internal", PasswordHash = passwordHash, Role = UserRole.Simulator, Tier = ServiceTier.None, DealerId = SeedConstants.DealerId },
        };

        await _context.Users.AddRangeAsync(users);
    }

    private static Vehicle BuildVehicle(Guid id, string registration, params EquipmentType[] equipmentTypes)
    {
        var vehicle = new Vehicle
        {
            Id = id,
            DealerId = SeedConstants.DealerId,
            Registration = registration,
            Equipment = equipmentTypes.Select(et => new VehicleEquipment { VehicleId = id, EquipmentType = et }).ToList()
        };
        return vehicle;
    }

    private static Vehicle BuildDealer2Vehicle(Guid id, string registration, params EquipmentType[] equipmentTypes)
    {
        var vehicle = new Vehicle
        {
            Id = id,
            DealerId = SeedConstants.Dealer2Id,
            Registration = registration,
            Equipment = equipmentTypes.Select(et => new VehicleEquipment { VehicleId = id, EquipmentType = et }).ToList()
        };
        return vehicle;
    }
}
