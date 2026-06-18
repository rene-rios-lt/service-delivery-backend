namespace ServiceDelivery.Application.Features.Dispatcher.Commands;

public record RedirectRepResult(
    Guid RepId,
    Guid FromRequestId,
    Guid ToRequestId,
    string RepState);
