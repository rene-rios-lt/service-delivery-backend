using MediatR;

namespace ServiceDelivery.Application.Features.Rep.Commands;

public record HeartbeatCommand(Guid RepId) : IRequest<HeartbeatResult>;
