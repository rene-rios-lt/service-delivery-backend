namespace ServiceDelivery.Domain.Exceptions;

public class InvalidRepStateException : DomainException
{
    public InvalidRepStateException(string message)
        : base(message)
    {
    }
}
