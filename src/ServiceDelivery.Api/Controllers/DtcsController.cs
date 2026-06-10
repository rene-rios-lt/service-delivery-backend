using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.Dtcs.Queries;

namespace ServiceDelivery.Api.Controllers;

[ApiController]
[Route("dtcs")]
[Authorize]
public class DtcsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DtcsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = "ServiceRep,Requester")]
    public async Task<IActionResult> GetDtcs()
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        var result = await _mediator.Send(new GetDtcsQuery(dealerId));
        return Ok(result);
    }
}
