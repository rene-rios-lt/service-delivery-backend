using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.Dispatcher.Commands;
using ServiceDelivery.Application.Features.Dispatcher.Queries;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Api.Controllers;

public record RedirectRepRequest(Guid RepId, Guid ToRequestId);

[ApiController]
[Route("dispatcher")]
[Authorize(Roles = "Dispatcher")]
public class DispatcherController : ControllerBase
{
    private readonly IMediator _mediator;

    public DispatcherController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("fleet")]
    [ProducesResponseType<IReadOnlyList<DispatcherFleetEntryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFleet()
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        var result = await _mediator.Send(new GetDispatcherFleetQuery(dealerId));
        return Ok(result);
    }

    [HttpPost("redirect")]
    [ProducesResponseType<RedirectRepResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Redirect([FromBody] RedirectRepRequest body)
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new RedirectRepCommand(dealerId, body.RepId, body.ToRequestId));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (RedirectNotAllowedException ex)
        {
            return UnprocessableEntity(new { reason = ex.Reason });
        }
    }
}
