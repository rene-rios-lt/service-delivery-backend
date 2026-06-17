using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Domain.Interfaces;

public interface IVehicleRepository
{
    Task<IReadOnlyList<FleetJobState>> GetFleetJobStateByDealerAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vehicle>> GetAllByDealerIdAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vehicle>> GetUnclaimedByDealerIdAsync(
        Guid dealerId,
        CancellationToken cancellationToken = default);

    Task<Vehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default);

    Task<Vehicle?> GetByClaimedRepIdAsync(Guid repId, CancellationToken cancellationToken = default);
}
