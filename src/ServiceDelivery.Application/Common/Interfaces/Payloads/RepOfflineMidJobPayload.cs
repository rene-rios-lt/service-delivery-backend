namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record RepOfflineMidJobPayload(
    Guid RepId,
    Guid RequestId,
    string RepName,
    string DtcTitle);
