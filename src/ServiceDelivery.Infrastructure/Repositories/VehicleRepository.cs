using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Infrastructure.Persistence;

namespace ServiceDelivery.Infrastructure.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _context;

    public VehicleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Vehicle>> GetAllByDealerIdAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Vehicles
            .Include(v => v.Equipment)
            .Where(v => v.DealerId == dealerId)
            .ToListAsync(cancellationToken);
    }
}
