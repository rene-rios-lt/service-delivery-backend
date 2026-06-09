namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record ServiceRequestAssignedPayload(
    Guid RequestId,
    Guid RepId,
    string RepName,
    double Eta);
