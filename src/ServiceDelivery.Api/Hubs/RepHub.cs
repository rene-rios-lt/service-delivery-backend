using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServiceDelivery.Application.Features.Rep.Commands;

namespace ServiceDelivery.Api.Hubs;

[Authorize(Roles = "ServiceRep,Simulator")]
public class RepHub : ServiceDeliveryHubBase
{
    private readonly IMediator _mediator;

    public RepHub(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override async Task OnConnectedAsync()
    {
        var dealerId = Context.User?.FindFirst("dealerId")?.Value;
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? Context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(dealerId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"dealer:{dealerId}");

        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"rep:{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var dealerIdClaim = Context.User?.FindFirst("dealerId")?.Value;
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? Context.User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdClaim, out var repId) && Guid.TryParse(dealerIdClaim, out var dealerId))
            await _mediator.Send(new RepWentOfflineCommand(repId, dealerId));

        await base.OnDisconnectedAsync(exception);
    }
}
