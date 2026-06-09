namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record JobOfferReceivedPayload(
    Guid OfferId,
    Guid RequestId,
    string RequesterName,
    string RequesterTier,
    string DtcTitle,
    double Latitude,
    double Longitude,
    double DistanceMiles,
    double EtaMinutes);
