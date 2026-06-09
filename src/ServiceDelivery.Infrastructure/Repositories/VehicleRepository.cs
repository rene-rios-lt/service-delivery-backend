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

    public async Task<IReadOnlyList<Vehicle>> GetUnclaimedByDealerIdAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Vehicles
            .Include(v => v.Equipment)
            .Where(v => v.DealerId == dealerId && v.ClaimedByRepId == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await _context.Vehicles
            .Include(v => v.Equipment)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);
    }

    public async Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        _context.Vehicles.Update(vehicle);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
