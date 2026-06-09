using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;
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
            existing.LastRedirectedAt = record.LastRedirectedAt;
            existing.UpdatedAt = record.UpdatedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
