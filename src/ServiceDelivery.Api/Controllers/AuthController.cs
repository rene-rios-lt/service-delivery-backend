using MediatR;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Common.Exceptions;
using ServiceDelivery.Application.Features.Auth.Commands;

namespace ServiceDelivery.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (UnauthorizedException)
        {
            return Unauthorized();
        }
    }
}
