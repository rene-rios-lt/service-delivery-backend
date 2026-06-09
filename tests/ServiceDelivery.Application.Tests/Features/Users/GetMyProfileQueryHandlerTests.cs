using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Exceptions;
using ServiceDelivery.Application.Features.Users.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Users;

public class GetMyProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetMyProfileQueryHandler _handler;

    public GetMyProfileQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetMyProfileQueryHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task GivenAnAuthenticatedUser_WhenGetMyProfileHandled_ThenReturnsFullProfileDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "Alex Dispatcher",
            Email = "alex@dealer.com",
            PasswordHash = "hashed",
            Role = UserRole.Dispatcher,
            Tier = ServiceTier.None,
            DealerId = dealerId
        };
        _userRepositoryMock
            .Setup(r => r.FindByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var query = new GetMyProfileQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.UserId.Should().Be(userId);
        result.Name.Should().Be("Alex Dispatcher");
        result.Role.Should().Be(UserRole.Dispatcher);
        result.Tier.Should().Be(ServiceTier.None);
        result.DealerId.Should().Be(dealerId);
    }

    [Fact]
    public async Task GivenASubClaimMatchingASeededUser_WhenGetMyProfileHandled_ThenReturnedUserIdMatchesSub()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "Rep One",
            Email = "rep1@dealer.com",
            PasswordHash = "hashed",
            Role = UserRole.ServiceRep,
            Tier = ServiceTier.None,
            DealerId = Guid.NewGuid()
        };
        _userRepositoryMock
            .Setup(r => r.FindByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var query = new GetMyProfileQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GivenAMissingSubClaim_WhenGetMyProfileHandled_ThenUnauthorizedExceptionIsThrown()
    {
        // Arrange
        var query = new GetMyProfileQuery(Guid.Empty);

        // Act
        var act = async () => await _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task GivenASubClaimWithNoMatchingUser_WhenGetMyProfileHandled_ThenUnauthorizedExceptionIsThrown()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.FindByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var query = new GetMyProfileQuery(unknownId);

        // Act
        var act = async () => await _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
