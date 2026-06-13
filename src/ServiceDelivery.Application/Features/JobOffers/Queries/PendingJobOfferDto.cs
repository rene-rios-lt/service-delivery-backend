namespace ServiceDelivery.Application.Features.JobOffers.Queries;

public record RequesterLocationDto(double Lat, double Lng);

public record PendingJobOfferDto(
    Guid OfferId,
    string RequesterName,
    string Tier,
    string DtcTitle,
    double? DistanceMiles,
    double? EtaMinutes,
    RequesterLocationDto RequesterLocation,
    DateTime ExpiresAt
);
