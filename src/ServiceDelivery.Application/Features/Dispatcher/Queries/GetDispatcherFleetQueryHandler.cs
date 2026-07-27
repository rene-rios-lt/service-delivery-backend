using MediatR;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Dispatcher.Queries;

public class GetDispatcherFleetQueryHandler
    : IRequestHandler<GetDispatcherFleetQuery, IReadOnlyList<DispatcherFleetEntryDto>>
{
    private readonly IVehicleRepository _vehicleRepository;

    public GetDispatcherFleetQueryHandler(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<IReadOnlyList<DispatcherFleetEntryDto>> Handle(
        GetDispatcherFleetQuery request,
        CancellationToken cancellationToken)
    {
        var entries = await _vehicleRepository.GetDispatcherFleetByDealerAsync(
            request.DealerId, cancellationToken);

        return entries.Select(e =>
        {
            LastPositionDto? lastPosition =
                e.LastLatitude.HasValue && e.LastLongitude.HasValue
                    ? new LastPositionDto(e.LastLatitude.Value, e.LastLongitude.Value)
                    : null;

            return new DispatcherFleetEntryDto(
                e.ClaimingRepId ?? Guid.Empty,
                e.RepName,
                e.ClaimingRepId.HasValue
                    ? (e.RepState ?? RepState.Offline).ToString()
                    : "Unassigned",
                e.VehicleId,
                e.Registration,
                lastPosition,
                e.ActiveRequestId,
                e.ActiveRequestTier?.ToString(),
                e.ActiveRequestTitle,
                e.HumanControlled,
                e.RedirectCooldownExpiresAt);
        }).ToList();
    }
}
