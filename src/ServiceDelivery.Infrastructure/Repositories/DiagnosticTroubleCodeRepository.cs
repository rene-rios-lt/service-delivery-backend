using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Infrastructure.Persistence;

namespace ServiceDelivery.Infrastructure.Repositories;

public class DiagnosticTroubleCodeRepository : IDiagnosticTroubleCodeRepository
{
    private readonly AppDbContext _context;

    public DiagnosticTroubleCodeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DiagnosticTroubleCode>> GetAllByDealerIdAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DiagnosticTroubleCodes
            .Where(d => d.DealerId == dealerId)
            .ToListAsync(cancellationToken);
    }

    public Task<DiagnosticTroubleCode?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _context.DiagnosticTroubleCodes
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }
}
