using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Infrastructure.Persistence.Configurations;

public class VehicleEquipmentConfiguration : IEntityTypeConfiguration<VehicleEquipment>
{
    public void Configure(EntityTypeBuilder<VehicleEquipment> builder)
    {
        builder.ToTable("VehicleEquipment");
        builder.HasKey(ve => new { ve.VehicleId, ve.EquipmentType });
        builder.Property(ve => ve.EquipmentType).HasConversion<string>();
    }
}
