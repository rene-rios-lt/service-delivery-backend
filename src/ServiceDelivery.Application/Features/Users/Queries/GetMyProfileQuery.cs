using MediatR;
using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Application.Features.Users.Queries;

public record GetMyProfileQuery(Guid UserId) : IRequest<UserProfileResult>;

public record UserProfileResult(
    Guid UserId,
    string Name,
    UserRole Role,
    ServiceTier Tier,
    Guid DealerId);
