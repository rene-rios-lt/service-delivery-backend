using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Domain.Interfaces;

public interface IRepStateRepository
{
    Task<RepStateRecord?> GetByRepIdAsync(Guid repId, CancellationToken cancellationToken = default);
    Task UpsertAsync(RepStateRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RepMatchCandidate>> GetAvailableByDealerAsync(Guid dealerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RepStateRecord>> GetStaleHumanControlledAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
