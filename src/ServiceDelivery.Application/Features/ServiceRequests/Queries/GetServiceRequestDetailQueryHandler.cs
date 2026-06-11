using MediatR;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public class GetServiceRequestDetailQueryHandler
    : IRequestHandler<GetServiceRequestDetailQuery, ServiceRequestDetailDto?>
{
    private readonly IServiceRequestRepository _serviceRequestRepository;

    public GetServiceRequestDetailQueryHandler(IServiceRequestRepository serviceRequestRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
    }

    public async Task<ServiceRequestDetailDto?> Handle(
        GetServiceRequestDetailQuery request,
        CancellationToken cancellationToken)
    {
        var detail = await _serviceRequestRepository
            .GetDetailByIdAsync(request.RequestId, request.DealerId, cancellationToken);

        if (detail is null)
            return null;

        if (!CallerMaySee(detail, request.CallerUserId, request.CallerRole))
            return null;

        var assignedRep = detail.AssignedRepId is null
            ? null
            : new AssignedRepDto(detail.AssignedRepId.Value, detail.AssignedRepName!);

        var offerHistory = detail.OfferHistory
            .Select(o => new JobOfferHistoryDto(
                o.OfferId,
                o.RepId,
                o.RepName,
                o.Status.ToString(),
                o.OfferedAt,
                o.ExpiresAt))
            .ToList();

        return new ServiceRequestDetailDto(
            detail.RequestId,
            detail.RequesterName,
            detail.Tier.ToString(),
            detail.DtcTitle,
            new LocationDto(detail.Latitude, detail.Longitude),
            detail.Status.ToString(),
            assignedRep,
            detail.CreatedAt,
            offerHistory);
    }

    private static bool CallerMaySee(ServiceRequestDetail detail, Guid callerUserId, UserRole callerRole)
        => callerRole switch
        {
            UserRole.Dispatcher => true,
            UserRole.Requester => detail.RequesterId == callerUserId,
            UserRole.ServiceRep => detail.AssignedRepId == callerUserId,
            _ => false
        };
}
