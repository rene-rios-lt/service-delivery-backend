using MediatR;
using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Application.Features.ServiceRequests.Commands;

public record SubmitServiceRequestCommand(
    Guid RequesterId,
    Guid DealerId,
    ServiceTier Tier,
    Guid DtcId,
    double Latitude,
    double Longitude) : IRequest<SubmitServiceRequestResult>;
