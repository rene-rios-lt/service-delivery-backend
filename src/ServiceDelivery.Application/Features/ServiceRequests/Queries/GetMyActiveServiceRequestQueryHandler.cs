using MediatR;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public class GetMyActiveServiceRequestQueryHandler
    : IRequestHandler<GetMyActiveServiceRequestQuery, MyActiveServiceRequestDto?>
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IDiagnosticTroubleCodeRepository _dtcRepository;

    public GetMyActiveServiceRequestQueryHandler(
        IServiceRequestRepository serviceRequestRepository,
        IDiagnosticTroubleCodeRepository dtcRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _dtcRepository = dtcRepository;
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

        return new MyActiveServiceRequestDto(
            serviceRequest.Id,
            serviceRequest.Tier.ToString(),
            dtc?.HumanReadableTitle ?? string.Empty,
            serviceRequest.Status.ToString(),
            serviceRequest.Latitude,
            serviceRequest.Longitude,
            serviceRequest.CreatedAt
        );
    }
}
