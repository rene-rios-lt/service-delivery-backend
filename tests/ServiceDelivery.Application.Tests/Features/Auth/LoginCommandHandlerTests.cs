using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common.Exceptions;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _passwordHasherMock = new Mock<IPasswordHasher>();

        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _jwtTokenServiceMock.Object,
            _passwordHasherMock.Object);
    }

    [Fact]
    public async Task GivenAValidCredential_WhenLoginCommandHandled_ThenTokenIsReturned()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "rep1@dealer.com",
            PasswordHash = "hashed",
            Role = UserRole.ServiceRep,
            Tier = ServiceTier.None,
            DealerId = Guid.NewGuid()
        };
        var command = new LoginCommand("rep1@dealer.com", "Password123!");

        _userRepositoryMock
            .Setup(r => r.FindByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);
        _jwtTokenServiceMock
            .Setup(j => j.GenerateToken(user))
            .Returns("signed.jwt.token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Token.Should().Be("signed.jwt.token");
    }

    [Fact]
    public async Task GivenAValidCredential_WhenLoginCommandHandled_ThenJwtContainsSubRoleTierDealerId()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "rep1@dealer.com",
            PasswordHash = "hashed",
            Role = UserRole.ServiceRep,
            Tier = ServiceTier.None,
            DealerId = Guid.NewGuid()
        };
        var command = new LoginCommand("rep1@dealer.com", "Password123!");

        _userRepositoryMock
            .Setup(r => r.FindByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);
        _jwtTokenServiceMock
            .Setup(j => j.GenerateToken(user))
            .Returns("signed.jwt.token");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _jwtTokenServiceMock.Verify(j => j.GenerateToken(It.Is<User>(u =>
            u.Id == user.Id &&
            u.Role == user.Role &&
            u.Tier == user.Tier &&
            u.DealerId == user.DealerId)), Times.Once);
    }

    [Fact]
    public async Task GivenAWrongPassword_WhenLoginCommandHandled_ThenUnauthorizedExceptionIsThrown()
    {
        // Arrange
        var user = new User
        {
            Email = "rep1@dealer.com",
            PasswordHash = "hashed",
            Role = UserRole.ServiceRep,
            Tier = ServiceTier.None
        };
        var command = new LoginCommand("rep1@dealer.com", "WrongPassword!");

        _userRepositoryMock
            .Setup(r => r.FindByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(false);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task GivenAnUnknownEmail_WhenLoginCommandHandled_ThenUnauthorizedExceptionIsThrown()
    {
        // Arrange
        var command = new LoginCommand("unknown@example.com", "Password123!");

        _userRepositoryMock
            .Setup(r => r.FindByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task GivenARequesterWithNoTier_WhenLoginCommandHandled_ThenUnauthorizedExceptionIsThrown()
    {
        // Arrange
        var user = new User
        {
            Email = "bronze1@example.com",
            PasswordHash = "hashed",
            Role = UserRole.Requester,
            Tier = ServiceTier.None
        };
        var command = new LoginCommand("bronze1@example.com", "Password123!");

        _userRepositoryMock
            .Setup(r => r.FindByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
