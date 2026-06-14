using MediatR;

namespace ServiceDelivery.Application.Features.Rep.Commands;

public record ArriveCommand(Guid RepId) : IRequest<ArriveResult>;
