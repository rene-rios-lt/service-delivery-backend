using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Infrastructure.Persistence;

namespace ServiceDelivery.Infrastructure.Repositories;

public class RepSessionRepository : IRepSessionRepository
{
    private readonly AppDbContext _context;

    public RepSessionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RepSession?> GetActiveByRepIdAsync(Guid repId, CancellationToken cancellationToken = default)
    {
        return await _context.RepSessions
            .FirstOrDefaultAsync(s => s.RepId == repId && s.EndedAt == null, cancellationToken);
    }

    public async Task AddAsync(RepSession session, CancellationToken cancellationToken = default)
    {
        await _context.RepSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RepSession session, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.RepSessions
            .FirstOrDefaultAsync(s => s.Id == session.Id, cancellationToken);

        if (tracked is not null)
            tracked.EndedAt = session.EndedAt;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
