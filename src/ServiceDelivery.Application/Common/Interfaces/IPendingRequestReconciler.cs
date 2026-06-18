namespace ServiceDelivery.Application.Common.Interfaces;

public interface IPendingRequestReconciler
{
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}
