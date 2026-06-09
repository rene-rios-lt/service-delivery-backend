using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleEquipment> VehicleEquipment => Set<VehicleEquipment>();
    public DbSet<DiagnosticTroubleCode> DiagnosticTroubleCodes => Set<DiagnosticTroubleCode>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<RepSession> RepSessions => Set<RepSession>();
    public DbSet<RepStateRecord> RepStateRecords => Set<RepStateRecord>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
