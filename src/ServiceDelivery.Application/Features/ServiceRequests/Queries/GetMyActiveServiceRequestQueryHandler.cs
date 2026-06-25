using MediatR;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public class GetMyActiveServiceRequestQueryHandler
    : IRequestHandler<GetMyActiveServiceRequestQuery, MyActiveServiceRequestDto?>
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IDiagnosticTroubleCodeRepository _dtcRepository;
    private readonly IRepStateRepository _repStateRepository;

    public GetMyActiveServiceRequestQueryHandler(
        IServiceRequestRepository serviceRequestRepository,
        IDiagnosticTroubleCodeRepository dtcRepository,
        IRepStateRepository repStateRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _dtcRepository = dtcRepository;
        _repStateRepository = repStateRepository;
    }

    public async Task<MyActiveServiceRequestDto?> Handle(
        GetMyActiveServiceRequestQuery request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = await _serviceRequestRepository
            .GetActiveByRepIdAsync(request.RepId, cancellationToken);

        if (serviceRequest is null)
            return null;

        var dtc = await _dtcRepository.GetByIdAsync(serviceRequest.DtcId, cancellationToken);

        // The rep's proximity state (EnRoute/Within15Miles/OnSite) is what drives the active-job UI's
        // "I've Arrived" enable rule — distinct from the request's lifecycle Status. A rep with an
        // active request is at least EnRoute, so default to that if the record is somehow absent.
        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken);

        return new MyActiveServiceRequestDto(
            serviceRequest.Id,
            serviceRequest.Tier.ToString(),
            dtc?.HumanReadableTitle ?? string.Empty,
            serviceRequest.Status.ToString(),
            (repState?.State ?? RepState.EnRoute).ToString(),
            serviceRequest.Latitude,
            serviceRequest.Longitude,
            serviceRequest.CreatedAt
        );
    }
}
