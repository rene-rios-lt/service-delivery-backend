using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}
