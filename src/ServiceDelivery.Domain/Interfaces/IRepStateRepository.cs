using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Interfaces;

public interface IRepStateRepository
{
    Task<RepStateRecord?> GetByRepIdAsync(Guid repId, CancellationToken cancellationToken = default);
    Task UpsertAsync(RepStateRecord record, CancellationToken cancellationToken = default);
}
