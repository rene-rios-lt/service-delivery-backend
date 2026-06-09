using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Infrastructure.Persistence.Configurations;

public class JobOfferConfiguration : IEntityTypeConfiguration<JobOffer>
{
    public void Configure(EntityTypeBuilder<JobOffer> builder)
    {
        builder.ToTable("JobOffers");
        builder.HasKey(jo => jo.Id);
        builder.Property(jo => jo.Status).HasConversion<string>();
    }
}
