using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Interfaces;

public interface IJobOfferRepository
{
    Task AddAsync(JobOffer offer, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetSkippedRepIdsForRequestAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<JobOffer?> GetPendingByRepIdAsync(Guid repId, CancellationToken cancellationToken = default);

    Task<JobOffer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(JobOffer offer, CancellationToken cancellationToken = default);
}
