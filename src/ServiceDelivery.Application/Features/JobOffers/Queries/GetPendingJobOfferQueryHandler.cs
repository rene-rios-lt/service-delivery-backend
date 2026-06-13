using MediatR;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.JobOffers.Queries;

public class GetPendingJobOfferQueryHandler
    : IRequestHandler<GetPendingJobOfferQuery, PendingJobOfferDto?>
{
    private readonly IJobOfferRepository _jobOfferRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IDiagnosticTroubleCodeRepository _dtcRepository;
    private readonly IUserRepository _userRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public GetPendingJobOfferQueryHandler(
        IJobOfferRepository jobOfferRepository,
        IServiceRequestRepository serviceRequestRepository,
        IDiagnosticTroubleCodeRepository dtcRepository,
        IUserRepository userRepository,
        IVehicleRepository vehicleRepository)
    {
        _jobOfferRepository = jobOfferRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _dtcRepository = dtcRepository;
        _userRepository = userRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<PendingJobOfferDto?> Handle(
        GetPendingJobOfferQuery request,
        CancellationToken cancellationToken)
    {
        var offer = await _jobOfferRepository.GetPendingByRepIdAsync(request.RepId, cancellationToken);
        if (offer is null)
            return null;

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(offer.ServiceRequestId, cancellationToken);
        if (serviceRequest is null)
            return null;

        var dtc = await _dtcRepository.GetByIdAsync(serviceRequest.DtcId, cancellationToken);
        var requester = await _userRepository.FindByIdAsync(serviceRequest.RequesterId, cancellationToken);

        double? distanceMiles = null;
        double? etaMinutes = null;
        var vehicle = await _vehicleRepository.GetByClaimedRepIdAsync(request.RepId, cancellationToken);
        if (vehicle is { LastLatitude: not null, LastLongitude: not null })
        {
            distanceMiles = HaversineCalculator.DistanceMiles(
                vehicle.LastLatitude.Value, vehicle.LastLongitude.Value,
                serviceRequest.Latitude, serviceRequest.Longitude);
            etaMinutes = HaversineCalculator.EtaMinutes(distanceMiles.Value);
        }

        return new PendingJobOfferDto(
            offer.Id,
            requester?.Name ?? string.Empty,
            serviceRequest.Tier.ToString(),
            dtc?.HumanReadableTitle ?? string.Empty,
            distanceMiles,
            etaMinutes,
            new RequesterLocationDto(serviceRequest.Latitude, serviceRequest.Longitude),
            offer.ExpiresAt);
    }
}
