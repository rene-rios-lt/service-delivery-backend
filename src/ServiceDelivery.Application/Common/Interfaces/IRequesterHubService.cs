using ServiceDelivery.Application.Common.Interfaces.Payloads;

namespace ServiceDelivery.Application.Common.Interfaces;

public interface IRequesterHubService
{
    Task SendRepAssignedAsync(string requesterUserGroup, RepAssignedPayload payload, CancellationToken ct = default);
    Task SendRepPositionUpdatedAsync(string requesterUserGroup, RepPositionUpdatedPayload payload, CancellationToken ct = default);
    Task SendRepRedirectedAsync(string requesterUserGroup, RepRedirectedPayload payload, CancellationToken ct = default);
    Task SendServiceCompletedAsync(string requesterUserGroup, ServiceCompletedPayload payload, CancellationToken ct = default);
    Task SendRepArrivedAsync(string requesterUserGroup, RepArrivedPayload payload, CancellationToken ct = default);
    Task SendRequestBackToPendingAsync(string requesterUserGroup, RequestBackToPendingPayload payload, CancellationToken ct = default);
}
