using ServiceDelivery.Application.Common.Interfaces;

namespace ServiceDelivery.Infrastructure.Services;

public class BcryptPasswordHasher : IPasswordHasher
{
    public bool Verify(string plaintext, string hash)
        => BCrypt.Net.BCrypt.Verify(plaintext, hash);
}
