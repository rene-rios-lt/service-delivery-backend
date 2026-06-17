using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.Simulator.Queries;

namespace ServiceDelivery.Api.Controllers;

[ApiController]
[Route("simulator")]
[Authorize(Roles = "Simulator")]
public class SimulatorController : ControllerBase
{
    private readonly IMediator _mediator;

    public SimulatorController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("fleet-state")]
    public async Task<IActionResult> GetFleetState()
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        var result = await _mediator.Send(new GetFleetStateQuery(dealerId));
        return Ok(result);
    }
}
