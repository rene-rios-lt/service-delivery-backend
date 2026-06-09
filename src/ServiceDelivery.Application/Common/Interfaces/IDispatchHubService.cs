using ServiceDelivery.Application.Common.Interfaces.Payloads;

namespace ServiceDelivery.Application.Common.Interfaces;

public interface IDispatchHubService
{
    Task SendServiceRequestPendingAsync(string dealerGroup, ServiceRequestPendingPayload payload, CancellationToken ct = default);
    Task SendServiceRequestAssignedAsync(string dealerGroup, ServiceRequestAssignedPayload payload, CancellationToken ct = default);
    Task SendServiceRequestCompletedAsync(string dealerGroup, ServiceRequestCompletedPayload payload, CancellationToken ct = default);
    Task SendRepStateChangedAsync(string dealerGroup, RepStateChangedPayload payload, CancellationToken ct = default);
    Task SendRepOfflineMidJobAsync(string dealerGroup, RepOfflineMidJobPayload payload, CancellationToken ct = default);
    Task SendFleetPositionUpdateAsync(string dealerGroup, FleetPositionUpdatePayload payload, CancellationToken ct = default);
}
