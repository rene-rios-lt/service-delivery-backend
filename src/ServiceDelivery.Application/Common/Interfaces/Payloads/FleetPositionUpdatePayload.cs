namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record FleetPositionUpdatePayload(
    Guid RepId,
    double Latitude,
    double Longitude,
    string State);
