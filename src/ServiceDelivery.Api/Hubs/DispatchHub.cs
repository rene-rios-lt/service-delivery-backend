using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ServiceDelivery.Api.Hubs;

[Authorize(Roles = "Dispatcher")]
public class DispatchHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var dealerId = Context.User?.FindFirst("dealerId")?.Value;
        if (!string.IsNullOrEmpty(dealerId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"dealer:{dealerId}");

        await base.OnConnectedAsync();
    }
}
