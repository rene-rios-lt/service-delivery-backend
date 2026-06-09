using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Infrastructure.Persistence.Configurations;

public class DiagnosticTroubleCodeConfiguration : IEntityTypeConfiguration<DiagnosticTroubleCode>
{
    public void Configure(EntityTypeBuilder<DiagnosticTroubleCode> builder)
    {
        builder.ToTable("DiagnosticTroubleCodes");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Code).IsRequired().HasMaxLength(20);
        builder.Property(d => d.HumanReadableTitle).IsRequired().HasMaxLength(200);
        builder.Property(d => d.RequiredEquipmentType).HasConversion<string>();
    }
}
