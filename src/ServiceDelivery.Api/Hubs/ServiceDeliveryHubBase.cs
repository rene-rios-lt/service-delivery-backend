using Microsoft.AspNetCore.SignalR;

namespace ServiceDelivery.Api.Hubs;

public abstract class ServiceDeliveryHubBase : Hub
{
    public Task Ping() => Clients.Caller.SendAsync("Pong");
}
