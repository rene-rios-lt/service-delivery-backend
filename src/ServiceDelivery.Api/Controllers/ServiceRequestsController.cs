using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDelivery.Application.Features.ServiceRequests.Commands;
using ServiceDelivery.Application.Features.ServiceRequests.Queries;
using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Api.Controllers;

public record SubmitServiceRequestBody(Guid DtcId, double Latitude, double Longitude);

[ApiController]
[Route("service-requests")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ServiceRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("my-active")]
    [Authorize(Roles = "ServiceRep")]
    [ProducesResponseType<MyActiveServiceRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyActiveServiceRequest()
    {
        var repIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(repIdClaim, out var repId))
            return Unauthorized();

        var result = await _mediator.Send(new GetMyActiveServiceRequestQuery(repId));

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ServiceRequestDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceRequestDetail(Guid id)
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var role = User.IsInRole("Dispatcher") ? UserRole.Dispatcher
            : User.IsInRole("ServiceRep") ? UserRole.ServiceRep
            : User.IsInRole("Requester") ? UserRole.Requester
            : (UserRole?)null;

        if (role is null)
            return NotFound();

        var result = await _mediator.Send(new GetServiceRequestDetailQuery(id, dealerId, userId, role.Value));

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "Dispatcher")]
    [ProducesResponseType<IReadOnlyList<ActiveServiceRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveServiceRequests()
    {
        var dealerIdClaim = User.FindFirstValue("dealerId");

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        var result = await _mediator.Send(new GetActiveServiceRequestsQuery(dealerId));

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Requester")]
    [ProducesResponseType<SubmitServiceRequestResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitServiceRequest([FromBody] SubmitServiceRequestBody body)
    {
        var requesterIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var dealerIdClaim = User.FindFirstValue("dealerId");
        var tierClaim = User.FindFirstValue("tier");

        if (!Guid.TryParse(requesterIdClaim, out var requesterId))
            return Unauthorized();

        if (!Guid.TryParse(dealerIdClaim, out var dealerId))
            return Unauthorized();

        if (!Enum.TryParse<ServiceTier>(tierClaim, out var tier))
            return Unauthorized();

        var result = await _mediator.Send(new SubmitServiceRequestCommand(
            RequesterId: requesterId,
            DealerId: dealerId,
            Tier: tier,
            DtcId: body.DtcId,
            Latitude: body.Latitude,
            Longitude: body.Longitude));

        // QUAL-013: return the typed result directly so [ProducesResponseType<SubmitServiceRequestResult>]
        // emits a response schema. Wire shape is identical to the former anonymous object — RequestId
        // and Status serialize to { "requestId": ..., "status": ... } under the camelCase policy.
        return Ok(result);
    }
}
