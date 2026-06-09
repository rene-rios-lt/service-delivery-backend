namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record VehiclePositionUpdatedPayload(
    Guid RepId,
    Guid VehicleId,
    double Latitude,
    double Longitude,
    string State);
