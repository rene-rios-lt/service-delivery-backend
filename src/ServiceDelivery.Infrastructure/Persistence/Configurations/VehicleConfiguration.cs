using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Infrastructure.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Registration).IsRequired().HasMaxLength(20);
        builder.Property(v => v.ClaimedByRepId).IsRequired(false);
        builder.Property(v => v.ClaimedAt).IsRequired(false);
        builder.Property(v => v.LastLatitude).IsRequired(false);
        builder.Property(v => v.LastLongitude).IsRequired(false);
        builder.Property(v => v.LastPositionUpdatedAt).IsRequired(false);
        builder.Property(v => v.RowVersion)
               .IsRowVersion()
               .HasDefaultValueSql("randomblob(8)");
        builder.HasMany(v => v.Equipment)
               .WithOne(e => e.Vehicle)
               .HasForeignKey(e => e.VehicleId);
    }
}
