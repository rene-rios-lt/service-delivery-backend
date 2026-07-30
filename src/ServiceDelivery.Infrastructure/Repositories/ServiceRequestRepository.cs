using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;
using ServiceDelivery.Infrastructure.Persistence;

namespace ServiceDelivery.Infrastructure.Repositories;

public class ServiceRequestRepository : IServiceRequestRepository
{
    private readonly AppDbContext _context;

    public ServiceRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
        => await _context.ServiceRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

    public async Task<ServiceRequest?> GetActiveByRepIdAsync(Guid repId, CancellationToken cancellationToken = default)
        => await _context.ServiceRequests
            .FirstOrDefaultAsync(
                r => r.AssignedRepId == repId
                     && (r.Status == ServiceRequestStatus.Assigned || r.Status == ServiceRequestStatus.InProgress),
                cancellationToken);

    public async Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        await _context.ServiceRequests.AddAsync(request, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        _context.ServiceRequests.Update(request);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceRequest>> GetPendingByDealerAsync(Guid dealerId, CancellationToken cancellationToken = default)
        // BUG-064: return the dealer's Pending set in tier-arbitration order so that when the matcher
        // serves them on a rep free-up (MatchingService.RunForPendingByDealerAsync), the highest-tier
        // request is offered first and a waiting Gold is never stranded behind a Silver. ServiceTier is
        // None=0, Bronze=1, Silver=2, Gold=3, so OrderByDescending(Tier) yields Gold→Silver→Bronze;
        // ThenBy(CreatedAt) breaks ties oldest-first (FCFS) within a tier.
        => await _context.ServiceRequests
            .Where(r => r.DealerId == dealerId && r.Status == ServiceRequestStatus.Pending)
            .OrderByDescending(r => r.Tier)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<ServiceRequestDetail?> GetDetailByIdAsync(Guid requestId, Guid dealerId, CancellationToken cancellationToken = default)
    {
        var projection = await _context.ServiceRequests
            .Where(r => r.Id == requestId && r.DealerId == dealerId)
            .Join(_context.Users,
                r => r.RequesterId,
                u => u.Id,
                (r, u) => new { Request = r, Requester = u })
            .Join(_context.DiagnosticTroubleCodes,
                x => x.Request.DtcId,
                d => d.Id,
                (x, d) => new { x.Request, x.Requester, Dtc = d })
            .GroupJoin(_context.Users,
                x => x.Request.AssignedRepId,
                rep => rep.Id,
                (x, reps) => new { x.Request, x.Requester, x.Dtc, Reps = reps })
            .SelectMany(
                x => x.Reps.DefaultIfEmpty(),
                (x, rep) => new
                {
                    x.Request,
                    x.Requester,
                    x.Dtc,
                    AssignedRepName = rep != null ? rep.Name : null
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (projection is null)
            return null;

        var offerHistory = await _context.JobOffers
            .Where(o => o.ServiceRequestId == requestId)
            .Join(_context.Users,
                o => o.RepId,
                u => u.Id,
                (o, u) => new JobOfferHistoryEntry(
                    o.Id,
                    o.RepId,
                    u.Name,
                    o.Status,
                    o.OfferedAt,
                    o.ExpiresAt))
            .OrderBy(o => o.OfferedAt)
            .ToListAsync(cancellationToken);

        return new ServiceRequestDetail(
            projection.Request.Id,
            projection.Request.RequesterId,
            projection.Requester.Name,
            projection.Request.Tier,
            projection.Dtc.HumanReadableTitle,
            projection.Request.Latitude,
            projection.Request.Longitude,
            projection.Request.Status,
            projection.Request.AssignedRepId,
            projection.AssignedRepName,
            projection.Request.CreatedAt,
            offerHistory);
    }

    public async Task<IReadOnlyList<ServiceRequest>> GetOrphanedPendingAsync(CancellationToken cancellationToken = default)
        => await _context.ServiceRequests
            .Where(r => r.Status == ServiceRequestStatus.Pending
                        && !_context.JobOffers.Any(o => o.ServiceRequestId == r.Id
                                                        && o.Status == JobOfferStatus.Pending))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServiceRequestSummary>> GetActiveByDealerIdAsync(Guid dealerId, CancellationToken cancellationToken = default)
    {
        return await _context.ServiceRequests
            .Where(r => r.DealerId == dealerId && r.Status != ServiceRequestStatus.Completed)
            .Join(_context.Users,
                r => r.RequesterId,
                u => u.Id,
                (r, u) => new { Request = r, Requester = u })
            .Join(_context.DiagnosticTroubleCodes,
                x => x.Request.DtcId,
                d => d.Id,
                (x, d) => new { x.Request, x.Requester, Dtc = d })
            .GroupJoin(_context.Users,
                x => x.Request.AssignedRepId,
                rep => rep.Id,
                (x, reps) => new { x.Request, x.Requester, x.Dtc, Reps = reps })
            .SelectMany(
                x => x.Reps.DefaultIfEmpty(),
                (x, rep) => new ServiceRequestSummary(
                    x.Request.Id,
                    x.Requester.Name,
                    x.Request.Tier,
                    x.Dtc.HumanReadableTitle,
                    x.Request.Status,
                    x.Request.AssignedRepId,
                    rep != null ? rep.Name : null,
                    x.Request.CreatedAt
                ))
            .ToListAsync(cancellationToken);
    }
}
