namespace ServiceDelivery.Domain.Exceptions;

public class RepAlreadyHasActiveSessionException : DomainException
{
    public RepAlreadyHasActiveSessionException(Guid repId)
        : base($"Rep {repId} already has an active session.")
    {
    }
}
