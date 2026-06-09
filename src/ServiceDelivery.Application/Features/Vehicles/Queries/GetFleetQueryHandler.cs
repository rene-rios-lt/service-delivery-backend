using MediatR;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Vehicles.Queries;

public class GetFleetQueryHandler : IRequestHandler<GetFleetQuery, IReadOnlyList<VehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;

    public GetFleetQueryHandler(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<IReadOnlyList<VehicleDto>> Handle(GetFleetQuery request, CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleRepository.GetAllByDealerIdAsync(request.DealerId, cancellationToken);

        return vehicles.Select(v =>
        {
            var state = v.ClaimedByRepId == null ? "Unclaimed" : "Claimed";

            var equipment = v.Equipment
                .Select(e => e.EquipmentType.ToString())
                .ToList();

            LastPositionDto? lastPosition = v.LastLatitude.HasValue
                ? new LastPositionDto(v.LastLatitude.Value, v.LastLongitude!.Value, v.LastPositionUpdatedAt!.Value)
                : null;

            return new VehicleDto(
                v.Id,
                v.Registration,
                state,
                v.ClaimedByRepId,
                equipment,
                lastPosition);
        }).ToList();
    }
}
