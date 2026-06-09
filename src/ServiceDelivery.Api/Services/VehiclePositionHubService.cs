using Microsoft.AspNetCore.SignalR;
using ServiceDelivery.Api.Hubs;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;

namespace ServiceDelivery.Api.Services;

public class VehiclePositionHubService : IVehiclePositionHubService
{
    private readonly IHubContext<VehiclePositionHub> _hubContext;

    public VehiclePositionHubService(IHubContext<VehiclePositionHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendVehiclePositionUpdatedAsync(
        string dealerGroup,
        VehiclePositionUpdatedPayload payload,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.Group(dealerGroup)
            .SendAsync("VehiclePositionUpdated", payload, cancellationToken);
    }
}
