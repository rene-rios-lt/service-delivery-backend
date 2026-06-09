using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.Vehicles.Commands;
using ServiceDelivery.Application.Features.Vehicles.Queries;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Api.Controllers;

[ApiController]
[Route("vehicles")]
[Authorize]
public class VehiclesController : ControllerBase
{
    private readonly IMediator _mediator;

    public VehiclesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = "Dispatcher")]
    public async Task<IActionResult> GetFleet()
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        var result = await _mediator.Send(new GetFleetQuery(dealerId));
        return Ok(result);
    }

    [HttpGet("available")]
    [Authorize(Roles = "ServiceRep")]
    public async Task<IActionResult> GetAvailableVehicles()
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        var result = await _mediator.Send(new GetAvailableVehiclesQuery(dealerId));
        return Ok(result);
    }

    [HttpPost("{id:guid}/claim")]
    [Authorize(Roles = "ServiceRep")]
    public async Task<IActionResult> ClaimVehicle(Guid id)
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new ClaimVehicleCommand(id, repId));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (VehicleAlreadyClaimedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (RepAlreadyHasActiveSessionException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
