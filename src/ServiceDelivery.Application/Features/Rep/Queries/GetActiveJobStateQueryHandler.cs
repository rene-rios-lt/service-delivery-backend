using MediatR;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Rep.Queries;

public class GetActiveJobStateQueryHandler
    : IRequestHandler<GetActiveJobStateQuery, ActiveJobStateDto?>
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDiagnosticTroubleCodeRepository _dtcRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRepStateRepository _repStateRepository;

    public GetActiveJobStateQueryHandler(
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository,
        IDiagnosticTroubleCodeRepository dtcRepository,
        IVehicleRepository vehicleRepository,
        IRepStateRepository repStateRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
        _dtcRepository = dtcRepository;
        _vehicleRepository = vehicleRepository;
        _repStateRepository = repStateRepository;
    }

    public async Task<ActiveJobStateDto?> Handle(GetActiveJobStateQuery request, CancellationToken cancellationToken)
    {
        var serviceRequest = await _serviceRequestRepository.GetActiveByRepIdAsync(request.RepId, cancellationToken);
        if (serviceRequest is null)
            return null;

        var requester = await _userRepository.FindByIdAsync(serviceRequest.RequesterId, cancellationToken);
        var dtc = await _dtcRepository.GetByIdAsync(serviceRequest.DtcId, cancellationToken);
        var vehicle = await _vehicleRepository.GetByClaimedRepIdAsync(request.RepId, cancellationToken);
        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken);

        var repLat = vehicle?.LastLatitude ?? 0.0;
        var repLng = vehicle?.LastLongitude ?? 0.0;
        var state = repState?.State ?? RepState.Offline;

        var etaMinutes = state == RepState.OnSite
            ? 0
            : (int)Math.Round(HaversineCalculator.EtaMinutes(
                HaversineCalculator.DistanceMiles(repLat, repLng, serviceRequest.Latitude, serviceRequest.Longitude)));

        return new ActiveJobStateDto(
            serviceRequest.Id,
            requester?.Name ?? string.Empty,
            dtc?.HumanReadableTitle ?? string.Empty,
            serviceRequest.Latitude,
            serviceRequest.Longitude,
            repLat,
            repLng,
            etaMinutes,
            state.ToString());
    }
}
