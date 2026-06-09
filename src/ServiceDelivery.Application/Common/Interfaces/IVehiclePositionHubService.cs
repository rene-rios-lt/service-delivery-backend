using ServiceDelivery.Application.Common.Interfaces.Payloads;

namespace ServiceDelivery.Application.Common.Interfaces;

public interface IVehiclePositionHubService
{
    Task SendVehiclePositionUpdatedAsync(
        string dealerGroup,
        VehiclePositionUpdatedPayload payload,
        CancellationToken cancellationToken = default);
}
