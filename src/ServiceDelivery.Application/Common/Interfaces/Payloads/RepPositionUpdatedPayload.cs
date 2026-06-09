namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record RepPositionUpdatedPayload(
    double Latitude,
    double Longitude,
    double EtaMinutes,
    string State);
