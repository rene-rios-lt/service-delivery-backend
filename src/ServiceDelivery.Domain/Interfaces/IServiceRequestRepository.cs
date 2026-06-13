using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Domain.Interfaces;

public interface IServiceRequestRepository
{
    Task<ServiceRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetActiveByRepIdAsync(Guid repId, CancellationToken cancellationToken = default);
    Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(ServiceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceRequest>> GetPendingByDealerAsync(Guid dealerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceRequestSummary>> GetActiveByDealerIdAsync(Guid dealerId, CancellationToken cancellationToken = default);
    Task<ServiceRequestDetail?> GetDetailByIdAsync(Guid requestId, Guid dealerId, CancellationToken cancellationToken = default);
}
