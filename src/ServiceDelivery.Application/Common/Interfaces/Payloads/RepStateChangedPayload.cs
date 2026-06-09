namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record RepStateChangedPayload(
    Guid RepId,
    string OldState,
    string NewState);
