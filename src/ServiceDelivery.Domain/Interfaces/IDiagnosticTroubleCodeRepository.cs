using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Interfaces;

public interface IDiagnosticTroubleCodeRepository
{
    Task<IReadOnlyList<DiagnosticTroubleCode>> GetAllByDealerIdAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default);
}
