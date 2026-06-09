using Microsoft.AspNetCore.SignalR;
using ServiceDelivery.Api.Hubs;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;

namespace ServiceDelivery.Api.Services;

public class DispatchHubService : IDispatchHubService
{
    private readonly IHubContext<DispatchHub> _hubContext;

    public DispatchHubService(IHubContext<DispatchHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendServiceRequestPendingAsync(string dealerGroup, ServiceRequestPendingPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(dealerGroup).SendAsync("ServiceRequestPending", payload, ct);

    public Task SendServiceRequestAssignedAsync(string dealerGroup, ServiceRequestAssignedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(dealerGroup).SendAsync("ServiceRequestAssigned", payload, ct);

    public Task SendServiceRequestCompletedAsync(string dealerGroup, ServiceRequestCompletedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(dealerGroup).SendAsync("ServiceRequestCompleted", payload, ct);

    public Task SendRepStateChangedAsync(string dealerGroup, RepStateChangedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(dealerGroup).SendAsync("RepStateChanged", payload, ct);

    public Task SendRepOfflineMidJobAsync(string dealerGroup, RepOfflineMidJobPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(dealerGroup).SendAsync("RepOfflineMidJob", payload, ct);

    public Task SendFleetPositionUpdateAsync(string dealerGroup, FleetPositionUpdatePayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(dealerGroup).SendAsync("FleetPositionUpdate", payload, ct);
}
