using MediatR;

namespace ServiceDelivery.Application.Features.JobOffers.Commands;

public record DeclineJobOfferCommand(Guid OfferId, Guid RepId) : IRequest<DeclineJobOfferResult>;
