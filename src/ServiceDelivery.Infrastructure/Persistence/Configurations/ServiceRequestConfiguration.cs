using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Infrastructure.Persistence.Configurations;

public class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("ServiceRequests");
        builder.HasKey(sr => sr.Id);
        builder.Property(sr => sr.Status).HasConversion<string>();
        builder.Property(sr => sr.Tier).HasConversion<string>();
        builder.Property(sr => sr.AssignedRepId).IsRequired(false);
    }
}
