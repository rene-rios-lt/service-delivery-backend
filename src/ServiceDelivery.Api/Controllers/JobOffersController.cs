using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.JobOffers.Commands;
using ServiceDelivery.Application.Features.JobOffers.Queries;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Api.Controllers;

[ApiController]
[Route("job-offers")]
[Authorize]
public class JobOffersController : ControllerBase
{
    private readonly IMediator _mediator;

    public JobOffersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("pending")]
    [Authorize(Roles = "ServiceRep")]
    [ProducesResponseType<PendingJobOfferDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingJobOffer()
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        var result = await _mediator.Send(new GetPendingJobOfferQuery(repId));

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = "ServiceRep")]
    [ProducesResponseType<AcceptJobOfferResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid id)
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new AcceptJobOfferCommand(id, repId));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidJobOfferStateException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (RepAlreadyOnActiveJobException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/decline")]
    [Authorize(Roles = "ServiceRep")]
    [ProducesResponseType<DeclineJobOfferResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Decline(Guid id)
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new DeclineJobOfferCommand(id, repId));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidJobOfferStateException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
