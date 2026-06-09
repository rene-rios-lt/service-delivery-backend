namespace ServiceDelivery.Application.Common.Interfaces;

public interface IPasswordHasher
{
    bool Verify(string plaintext, string hash);
}
