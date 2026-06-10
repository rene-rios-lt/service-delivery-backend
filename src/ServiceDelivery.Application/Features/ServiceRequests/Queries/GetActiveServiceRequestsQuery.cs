using MediatR;

namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public record GetActiveServiceRequestsQuery(Guid DealerId) : IRequest<IReadOnlyList<ActiveServiceRequestDto>>;
