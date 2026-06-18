using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;
using ServiceDelivery.Infrastructure.Persistence;

namespace ServiceDelivery.Infrastructure.Repositories;

public class RepStateRepository : IRepStateRepository
{
    private readonly AppDbContext _context;

    public RepStateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RepStateRecord?> GetByRepIdAsync(Guid repId, CancellationToken cancellationToken = default)
    {
        return await _context.RepStateRecords
            .FirstOrDefaultAsync(r => r.RepId == repId, cancellationToken);
    }

    public async Task UpsertAsync(RepStateRecord record, CancellationToken cancellationToken = default)
    {
        var existing = await _context.RepStateRecords
            .FirstOrDefaultAsync(r => r.RepId == record.RepId, cancellationToken);

        if (existing is null)
            await _context.RepStateRecords.AddAsync(record, cancellationToken);
        else
        {
            existing.State = record.State;
            existing.ActiveRequestId = record.ActiveRequestId;
            existing.HumanControlled = record.HumanControlled;
            existing.LastRedirectedAt = record.LastRedirectedAt;
            existing.LastHeartbeatAt = record.LastHeartbeatAt;
            existing.UpdatedAt = record.UpdatedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RepStateRecord>> GetStaleHumanControlledAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        return await _context.RepStateRecords
            .Where(r => r.HumanControlled
                        && (r.LastHeartbeatAt == null || r.LastHeartbeatAt < olderThan))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RepMatchCandidate>> GetAvailableByDealerAsync(Guid dealerId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.RepStateRecords
            .Where(rsr => rsr.State == RepState.Available)
            .Join(_context.RepSessions.Where(s => s.EndedAt == null),
                rsr => rsr.RepId,
                session => session.RepId,
                (rsr, session) => new { rsr, session })
            .Join(_context.Vehicles.Where(v => v.DealerId == dealerId
                                               && v.LastLatitude != null
                                               && v.LastLongitude != null),
                x => x.session.VehicleId,
                vehicle => vehicle.Id,
                (x, vehicle) => new
                {
                    x.rsr.RepId,
                    Latitude = vehicle.LastLatitude!.Value,
                    Longitude = vehicle.LastLongitude!.Value,
                    Equipment = vehicle.Equipment.Select(e => e.EquipmentType).ToList(),
                    AvailableSince = x.rsr.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new RepMatchCandidate(
                r.RepId,
                r.Latitude,
                r.Longitude,
                r.Equipment,
                r.AvailableSince))
            .ToList();
    }
}
