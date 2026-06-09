using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ServiceDelivery.Api.Hubs;

[Authorize(Roles = "ServiceRep,Simulator")]
public class RepHub : Hub
{
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

}
