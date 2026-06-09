using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
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
}
