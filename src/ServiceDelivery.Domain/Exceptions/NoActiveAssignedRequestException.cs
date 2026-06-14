namespace ServiceDelivery.Domain.Exceptions;

public class NoActiveAssignedRequestException : DomainException
{
    public NoActiveAssignedRequestException(Guid repId)
        : base($"Rep {repId} has no active assigned request to arrive at.")
    {
    }
}
