using MediatR;
using ServiceDelivery.Application.Common.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Users.Queries;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, UserProfileResult>
{
    private readonly IUserRepository _userRepository;

    public GetMyProfileQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserProfileResult> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new UnauthorizedException("Authenticated user identifier is missing.");

        var user = await _userRepository.FindByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            throw new UnauthorizedException("Authenticated user not found.");

        return new UserProfileResult(user.Id, user.Name, user.Role, user.Tier, user.DealerId);
    }
}
