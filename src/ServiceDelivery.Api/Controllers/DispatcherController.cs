using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.Dispatcher.Queries;

namespace ServiceDelivery.Api.Controllers;

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
    public async Task<IActionResult> GetFleet()
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        var result = await _mediator.Send(new GetDispatcherFleetQuery(dealerId));
        return Ok(result);
    }
}
