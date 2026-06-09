namespace ServiceDelivery.Domain.Exceptions;

public class VehicleAlreadyClaimedException : DomainException
{
    public VehicleAlreadyClaimedException(Guid vehicleId)
        : base($"Vehicle {vehicleId} is already claimed.")
    {
    }
}
