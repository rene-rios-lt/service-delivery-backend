namespace ServiceDelivery.Application.Common.Interfaces.Payloads;

public record RepAssignedPayload(
    Guid RepId,
    string RepName,
    double EtaMinutes,
    double Latitude,
    double Longitude);
