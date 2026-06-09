using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.Vehicles.Queries;

namespace ServiceDelivery.Api.Controllers;

[ApiController]
[Route("vehicles")]
[Authorize(Roles = "Dispatcher")]
public class VehiclesController : ControllerBase
{
    private readonly IMediator _mediator;

    public VehiclesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetFleet()
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        var result = await _mediator.Send(new GetFleetQuery(dealerId));
        return Ok(result);
    }
}
