using MediatR;

namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public record GetMyActiveServiceRequestQuery(Guid RepId) : IRequest<MyActiveServiceRequestDto?>;
