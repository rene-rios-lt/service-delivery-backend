using MediatR;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Vehicles.Queries;

public class GetAvailableVehiclesQueryHandler : IRequestHandler<GetAvailableVehiclesQuery, IReadOnlyList<AvailableVehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;

    public GetAvailableVehiclesQueryHandler(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<IReadOnlyList<AvailableVehicleDto>> Handle(
        GetAvailableVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleRepository.GetUnclaimedByDealerIdAsync(request.DealerId, cancellationToken);

        return vehicles.Select(v => new AvailableVehicleDto(
            v.Id,
            v.Registration,
            v.Equipment.Select(e => e.EquipmentType.ToString()).ToList()
        )).ToList();
    }
}
