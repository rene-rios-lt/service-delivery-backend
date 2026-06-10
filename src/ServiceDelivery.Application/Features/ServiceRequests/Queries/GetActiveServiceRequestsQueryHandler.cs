using MediatR;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public class GetActiveServiceRequestsQueryHandler : IRequestHandler<GetActiveServiceRequestsQuery, IReadOnlyList<ActiveServiceRequestDto>>
{
    private readonly IServiceRequestRepository _repository;

    public GetActiveServiceRequestsQueryHandler(IServiceRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ActiveServiceRequestDto>> Handle(GetActiveServiceRequestsQuery request, CancellationToken cancellationToken)
    {
        var summaries = await _repository.GetActiveByDealerIdAsync(request.DealerId, cancellationToken);

        return summaries.Select(s => new ActiveServiceRequestDto(
            s.RequestId,
            s.RequesterName,
            s.Tier.ToString(),
            s.DtcTitle,
            s.Status.ToString(),
            s.AssignedRepId,
            s.AssignedRepName,
            s.CreatedAt
        )).ToList();
    }
}
