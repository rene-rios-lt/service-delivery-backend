using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Infrastructure.Persistence.Configurations;

public class RepStateRecordConfiguration : IEntityTypeConfiguration<RepStateRecord>
{
    public void Configure(EntityTypeBuilder<RepStateRecord> builder)
    {
        builder.ToTable("RepStateRecords");
        builder.HasKey(rsr => rsr.RepId);
        builder.Property(rsr => rsr.State).HasConversion<string>();
        builder.Property(rsr => rsr.ActiveRequestId).IsRequired(false);
        builder.Property(rsr => rsr.LastRedirectedAt).IsRequired(false);
    }
}
