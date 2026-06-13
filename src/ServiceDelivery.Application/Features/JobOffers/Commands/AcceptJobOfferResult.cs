namespace ServiceDelivery.Application.Features.JobOffers.Commands;

public record AcceptJobOfferResult(
    Guid OfferId,
    Guid RequestId,
    string OfferStatus,
    string RequestStatus,
    string RepState);
