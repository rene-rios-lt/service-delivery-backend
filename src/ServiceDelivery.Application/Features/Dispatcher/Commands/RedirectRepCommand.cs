using MediatR;

namespace ServiceDelivery.Application.Features.Dispatcher.Commands;

public record RedirectRepCommand(Guid DealerId, Guid RepId, Guid ToRequestId) : IRequest<RedirectRepResult>;
