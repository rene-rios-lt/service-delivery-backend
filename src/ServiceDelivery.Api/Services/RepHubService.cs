using Microsoft.AspNetCore.SignalR;
using ServiceDelivery.Api.Hubs;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;

namespace ServiceDelivery.Api.Services;

public class RepHubService : IRepHubService
{
    private readonly IHubContext<RepHub> _hubContext;

    public RepHubService(IHubContext<RepHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendJobOfferReceivedAsync(string repUserGroup, JobOfferReceivedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(repUserGroup).SendAsync("JobOfferReceived", payload, ct);

    public Task SendJobOfferExpiredAsync(string repUserGroup, JobOfferExpiredPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(repUserGroup).SendAsync("JobOfferExpired", payload, ct);

    public Task SendRedirectReceivedAsync(string repUserGroup, RedirectReceivedPayload payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(repUserGroup).SendAsync("RedirectReceived", payload, ct);
}
