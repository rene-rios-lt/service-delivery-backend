using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
