using MediatR;
using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public record GetServiceRequestDetailQuery(
    Guid RequestId,
    Guid DealerId,
    Guid CallerUserId,
    UserRole CallerRole) : IRequest<ServiceRequestDetailDto?>;
