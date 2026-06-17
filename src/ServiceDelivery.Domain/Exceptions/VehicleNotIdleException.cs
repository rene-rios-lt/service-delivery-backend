namespace ServiceDelivery.Domain.Exceptions;

public class VehicleNotIdleException : DomainException
{
    public VehicleNotIdleException(string reason)
        : base(reason)
    {
    }
}
