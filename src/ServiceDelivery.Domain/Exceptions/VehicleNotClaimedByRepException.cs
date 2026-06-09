namespace ServiceDelivery.Domain.Exceptions;

public class VehicleNotClaimedByRepException : DomainException
{
    public VehicleNotClaimedByRepException(Guid vehicleId)
        : base($"Vehicle {vehicleId} is not claimed by the requesting rep.")
    {
    }
}
