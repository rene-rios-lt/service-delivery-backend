using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public ServiceTier Tier { get; set; } = ServiceTier.None;
    public Guid DealerId { get; set; }
}
