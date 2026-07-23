namespace ServiceDelivery.Domain.Exceptions;

public class RepAlreadyOnActiveJobException : DomainException
{
    public RepAlreadyOnActiveJobException(string reason)
        : base(reason)
    {
    }
}
