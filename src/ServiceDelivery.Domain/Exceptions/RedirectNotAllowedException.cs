namespace ServiceDelivery.Domain.Exceptions;

public class RedirectNotAllowedException : DomainException
{
    public string Reason { get; }

    public RedirectNotAllowedException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }
}
