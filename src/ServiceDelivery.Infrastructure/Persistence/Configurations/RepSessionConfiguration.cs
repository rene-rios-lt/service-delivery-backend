using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Infrastructure.Persistence.Configurations;

public class RepSessionConfiguration : IEntityTypeConfiguration<RepSession>
{
    public void Configure(EntityTypeBuilder<RepSession> builder)
    {
        builder.ToTable("RepSessions");
        builder.HasKey(rs => rs.Id);
        builder.Property(rs => rs.EndedAt).IsRequired(false);
    }
}
