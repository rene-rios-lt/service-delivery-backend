namespace ServiceDelivery.Application.Features.Rep.Commands;

public record ArriveResult(
    Guid RepId,
    Guid RequestId,
    string RepState,
    string RequestStatus);
