using MediatR;

namespace ServiceDelivery.Application.Features.JobOffers.Commands;

public record AcceptJobOfferCommand(Guid OfferId, Guid RepId) : IRequest<AcceptJobOfferResult>;
