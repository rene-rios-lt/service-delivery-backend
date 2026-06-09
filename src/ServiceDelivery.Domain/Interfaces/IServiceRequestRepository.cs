using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Interfaces;

public interface IServiceRequestRepository
{
    Task<ServiceRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetActiveByRepIdAsync(Guid repId, CancellationToken cancellationToken = default);
}
