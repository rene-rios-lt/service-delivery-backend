using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Interfaces;

public interface IVehicleRepository
{
    Task<IReadOnlyList<Vehicle>> GetAllByDealerIdAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default);
}
