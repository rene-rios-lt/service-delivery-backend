using MediatR;

namespace ServiceDelivery.Application.Features.Rep.Commands;

public record CompleteCommand(Guid RepId) : IRequest<CompleteResult>;
