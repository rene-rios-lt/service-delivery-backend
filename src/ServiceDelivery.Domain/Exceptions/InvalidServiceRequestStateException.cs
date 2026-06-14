namespace ServiceDelivery.Domain.Exceptions;

public class InvalidServiceRequestStateException : DomainException
{
    public InvalidServiceRequestStateException(string message)
        : base(message)
    {
    }
}
