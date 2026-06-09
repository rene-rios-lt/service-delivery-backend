using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Name).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>();
        builder.Property(u => u.Tier).HasConversion<string>();
    }
}
