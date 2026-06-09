namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record ServiceRequestPendingPayload(
    Guid RequestId,
    string RequesterTier,
    string DtcTitle,
    string Location);
