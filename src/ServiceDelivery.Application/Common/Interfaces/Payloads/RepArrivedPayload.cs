namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record RepArrivedPayload(
    Guid RepId,
    Guid RequestId);
