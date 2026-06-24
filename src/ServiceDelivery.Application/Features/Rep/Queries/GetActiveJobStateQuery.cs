using MediatR;

namespace ServiceDelivery.Application.Features.Rep.Queries;

public record GetActiveJobStateQuery(Guid RepId) : IRequest<ActiveJobStateDto?>;
