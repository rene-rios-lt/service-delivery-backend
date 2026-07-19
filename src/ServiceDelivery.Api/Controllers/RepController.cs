using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.Rep.Commands;
using ServiceDelivery.Application.Features.Rep.Queries;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Api.Controllers;

[ApiController]
[Route("rep")]
[Authorize]
public class RepController : ControllerBase
{
    private readonly IMediator _mediator;

    public RepController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("arrive")]
    [Authorize(Roles = "ServiceRep")]
    [ProducesResponseType<ArriveResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Arrive()
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new ArriveCommand(repId));
            return Ok(result);
        }
        catch (NoActiveAssignedRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidRepStateException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidServiceRequestStateException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("active-job-state")]
    [Authorize(Roles = "ServiceRep")]
    [ProducesResponseType<ActiveJobStateDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveJobState()
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        var result = await _mediator.Send(new GetActiveJobStateQuery(repId));

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("heartbeat")]
    [Authorize(Roles = "ServiceRep")]
    [ProducesResponseType<HeartbeatResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Heartbeat()
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new HeartbeatCommand(repId));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("complete")]
    [Authorize(Roles = "ServiceRep")]
    [ProducesResponseType<CompleteResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete()
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new CompleteCommand(repId));
            return Ok(result);
        }
        catch (NoActiveAssignedRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidRepStateException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidServiceRequestStateException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
