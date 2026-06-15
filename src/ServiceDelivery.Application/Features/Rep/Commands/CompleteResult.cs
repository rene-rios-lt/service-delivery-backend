namespace ServiceDelivery.Application.Features.Rep.Commands;

public record CompleteResult(
    Guid RepId,
    Guid RequestId,
    string RepState,
    string RequestStatus);
