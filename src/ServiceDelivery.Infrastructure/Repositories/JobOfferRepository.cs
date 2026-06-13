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
        return await _context.JobOffers
            .Where(o => o.ServiceRequestId == serviceRequestId
                        && (o.Status == JobOfferStatus.Declined || o.Status == JobOfferStatus.Expired))
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
}
