namespace ServiceDelivery.Domain.Exceptions;

public class VehicleReleaseBlockedByActiveJobException : DomainException
{
    public VehicleReleaseBlockedByActiveJobException(Guid repId)
        : base($"Rep {repId} cannot release a vehicle while an InProgress job is active.")
    {
    }
}
