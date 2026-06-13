namespace ServiceDelivery.Domain.Exceptions;

public class InvalidJobOfferStateException : DomainException
{
    public InvalidJobOfferStateException(string message)
        : base(message)
    {
    }
}
