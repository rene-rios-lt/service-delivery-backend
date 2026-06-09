using ServiceDelivery.Application.Common.Interfaces.Payloads;

namespace ServiceDelivery.Application.Common.Interfaces;

public interface IRepHubService
{
    Task SendJobOfferReceivedAsync(string repUserGroup, JobOfferReceivedPayload payload, CancellationToken ct = default);
    Task SendJobOfferExpiredAsync(string repUserGroup, JobOfferExpiredPayload payload, CancellationToken ct = default);
    Task SendRedirectReceivedAsync(string repUserGroup, RedirectReceivedPayload payload, CancellationToken ct = default);
}
