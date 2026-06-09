namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record VehicleForceReleasedPayload(Guid VehicleId, string Registration);
