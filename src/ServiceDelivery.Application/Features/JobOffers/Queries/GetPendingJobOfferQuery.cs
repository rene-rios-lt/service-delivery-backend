using MediatR;

namespace ServiceDelivery.Application.Features.JobOffers.Queries;

public record GetPendingJobOfferQuery(Guid RepId) : IRequest<PendingJobOfferDto?>;
