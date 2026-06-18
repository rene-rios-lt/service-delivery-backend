using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;
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

    public async Task<Vehicle?> GetByClaimedRepIdAsync(Guid repId, CancellationToken cancellationToken = default)
    {
        return await _context.Vehicles
            .FirstOrDefaultAsync(v => v.ClaimedByRepId == repId, cancellationToken);
    }

    public async Task<IReadOnlyList<DispatcherFleetEntry>> GetDispatcherFleetByDealerAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Vehicles
            .Where(v => v.DealerId == dealerId)
            .GroupJoin(_context.RepStateRecords,
                v => v.ClaimedByRepId,
                rs => (Guid?)rs.RepId,
                (v, states) => new { Vehicle = v, States = states })
            .SelectMany(
                x => x.States.DefaultIfEmpty(),
                (x, state) => new { x.Vehicle, State = state })
            .GroupJoin(_context.Users,
                x => x.Vehicle.ClaimedByRepId,
                u => (Guid?)u.Id,
                (x, users) => new { x.Vehicle, x.State, Users = users })
            .SelectMany(
                x => x.Users.DefaultIfEmpty(),
                (x, user) => new { x.Vehicle, x.State, User = user })
            .GroupJoin(_context.ServiceRequests,
                x => x.State != null ? x.State.ActiveRequestId : null,
                req => (Guid?)req.Id,
                (x, requests) => new { x.Vehicle, x.State, x.User, Requests = requests })
            .SelectMany(
                x => x.Requests.DefaultIfEmpty(),
                (x, request) => new DispatcherFleetEntry(
                    x.Vehicle.Id,
                    x.Vehicle.Registration,
                    x.Vehicle.ClaimedByRepId,
                    x.User != null ? x.User.Name : null,
                    x.State != null ? (RepState?)x.State.State : null,
                    x.State != null && x.State.HumanControlled,
                    x.Vehicle.LastLatitude,
                    x.Vehicle.LastLongitude,
                    request != null ? (Guid?)request.Id : null,
                    request != null ? (ServiceTier?)request.Tier : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FleetJobState>> GetFleetJobStateByDealerAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Vehicles
            .Where(v => v.DealerId == dealerId)
            .GroupJoin(_context.RepStateRecords,
                v => v.ClaimedByRepId,
                rs => (Guid?)rs.RepId,
                (v, states) => new { Vehicle = v, States = states })
            .SelectMany(
                x => x.States.DefaultIfEmpty(),
                (x, state) => new { x.Vehicle, State = state })
            .GroupJoin(_context.ServiceRequests,
                x => x.State != null ? x.State.ActiveRequestId : null,
                req => (Guid?)req.Id,
                (x, requests) => new { x.Vehicle, x.State, Requests = requests })
            .SelectMany(
                x => x.Requests.DefaultIfEmpty(),
                (x, request) => new FleetJobState(
                    x.Vehicle.Id,
                    x.Vehicle.ClaimedByRepId,
                    x.State != null ? (RepState?)x.State.State : null,
                    x.State != null && x.State.HumanControlled,
                    request != null ? (double?)request.Latitude : null,
                    request != null ? (double?)request.Longitude : null))
            .ToListAsync(cancellationToken);
    }
}
