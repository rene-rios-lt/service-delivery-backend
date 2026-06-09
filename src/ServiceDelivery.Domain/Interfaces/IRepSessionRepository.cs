using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Interfaces;

public interface IRepSessionRepository
{
    Task<RepSession?> GetActiveByRepIdAsync(Guid repId, CancellationToken cancellationToken = default);
    Task AddAsync(RepSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(RepSession session, CancellationToken cancellationToken = default);
}
