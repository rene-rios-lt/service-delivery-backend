using Microsoft.AspNetCore.SignalR;
using ServiceDelivery.Api.Hubs;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;

namespace ServiceDelivery.Api.Services;

public class RequesterHubService : IRequesterHubService
{
    private readonly IHubContext<RequesterHub> _hubContext;

    public RequesterHubService(IHubContext<RequesterHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendRepAssignedAsync(string requesterUserGroup, RepAssignedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(requesterUserGroup).SendAsync("RepAssigned", payload, ct);

    public Task SendRepPositionUpdatedAsync(string requesterUserGroup, RepPositionUpdatedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(requesterUserGroup).SendAsync("RepPositionUpdated", payload, ct);

    public Task SendRepRedirectedAsync(string requesterUserGroup, RepRedirectedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(requesterUserGroup).SendAsync("RepRedirected", payload, ct);

    public Task SendServiceCompletedAsync(string requesterUserGroup, ServiceCompletedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(requesterUserGroup).SendAsync("ServiceCompleted", payload, ct);
}
