namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record RepRedirectedPayload(
    string OldRepName,
    string NewRepName,
    double NewEtaMinutes);
