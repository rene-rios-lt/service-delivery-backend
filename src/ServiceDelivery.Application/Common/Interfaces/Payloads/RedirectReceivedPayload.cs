namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record RedirectReceivedPayload(
    Guid NewRequestId,
    string RequesterName,
    string RequesterTier,
    string DtcTitle,
    double Latitude,
    double Longitude,
    double DistanceMiles,
    double EtaMinutes);
