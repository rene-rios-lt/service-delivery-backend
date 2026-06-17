using MediatR;

namespace ServiceDelivery.Application.Features.Rep.Commands;

public record RepWentOfflineCommand(Guid RepId, Guid DealerId) : IRequest;
