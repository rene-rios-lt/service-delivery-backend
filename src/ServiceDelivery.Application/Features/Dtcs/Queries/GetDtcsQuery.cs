using MediatR;

namespace ServiceDelivery.Application.Features.Dtcs.Queries;

public record GetDtcsQuery(Guid DealerId) : IRequest<IReadOnlyList<DtcDto>>;
