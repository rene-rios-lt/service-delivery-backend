namespace ServiceDelivery.Application.Features.JobOffers.Commands;

public record DeclineJobOfferResult(
    Guid OfferId,
    Guid RequestId,
    string OfferStatus);
