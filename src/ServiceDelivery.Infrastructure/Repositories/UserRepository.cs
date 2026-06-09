using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Infrastructure.Persistence;

namespace ServiceDelivery.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
}
