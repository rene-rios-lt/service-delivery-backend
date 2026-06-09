using MediatR;

namespace ServiceDelivery.Application.Features.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public record LoginResult(string Token);
