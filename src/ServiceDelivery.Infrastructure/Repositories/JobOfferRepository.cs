using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Infrastructure.Persistence;

namespace ServiceDelivery.Infrastructure.Repositories;

public class JobOfferRepository : IJobOfferRepository
{
    private readonly AppDbContext _context;

    public JobOfferRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(JobOffer offer, CancellationToken cancellationToken = default)
    {
        await _context.JobOffers.AddAsync(offer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetSkippedRepIdsForRequestAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        // BUG-054: only an explicit Decline permanently skips a rep for a request. An Expired offer is a
        // missed notification, not an opt-out — the rep re-qualifies on the next matching run. Including
        // Expired here permanently poisoned the skip list, starving requests whose reps all let offers lapse.
        return await _context.JobOffers
            .Where(o => o.ServiceRequestId == serviceRequestId
                        && o.Status == JobOfferStatus.Declined)
            .Select(o => o.RepId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<JobOffer?> GetPendingByRepIdAsync(Guid repId, CancellationToken cancellationToken = default)
    {
        return await _context.JobOffers
            .Where(o => o.RepId == repId && o.Status == JobOfferStatus.Pending)
            .OrderByDescending(o => o.OfferedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<JobOffer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.JobOffers
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<JobOffer>> GetExpiredPendingAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default)
    {
        return await _context.JobOffers
            .Where(o => o.Status == JobOfferStatus.Pending && o.ExpiresAt <= asOf)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(JobOffer offer, CancellationToken cancellationToken = default)
    {
        _context.JobOffers.Update(offer);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
