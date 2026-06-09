using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Common.Exceptions;
using ServiceDelivery.Application.Features.Users.Queries;

namespace ServiceDelivery.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new GetMyProfileQuery(userId));
            return Ok(result);
        }
        catch (UnauthorizedException)
        {
            return Unauthorized();
        }
    }
}
