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
