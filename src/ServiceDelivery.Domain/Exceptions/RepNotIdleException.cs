namespace ServiceDelivery.Domain.Exceptions;

public class RepNotIdleException : DomainException
{
    public RepNotIdleException(string reason)
        : base(reason)
    {
    }
}
