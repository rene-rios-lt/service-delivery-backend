using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.Entities;

public class UserEntityTests
{
    [Fact]
    public void GivenANewUser_WhenConstructed_ThenDefaultTierIsNone()
    {
        // Arrange
        var user = new User();

        // Act
        var tier = user.Tier;

        // Assert
        tier.Should().Be(ServiceTier.None);
    }

    [Fact]
    public void GivenANewUser_WhenConstructed_ThenDefaultNameIsEmpty()
    {
        // Arrange
        var user = new User();

        // Act
        var name = user.Name;

        // Assert
        name.Should().BeEmpty();
    }

    [Fact]
    public void GivenANewUser_WhenConstructed_ThenDefaultEmailIsEmpty()
    {
        // Arrange
        var user = new User();

        // Act
        var email = user.Email;

        // Assert
        email.Should().BeEmpty();
    }

    [Fact]
    public void GivenAUserWithDispatcherRole_WhenRoleSet_ThenRoleIsDispatcher()
    {
        // Arrange
        var user = new User();

        // Act
        user.Role = UserRole.Dispatcher;

        // Assert
        user.Role.Should().Be(UserRole.Dispatcher);
    }

    [Fact]
    public void GivenAUserWithServiceRepRole_WhenRoleSet_ThenRoleIsServiceRep()
    {
        // Arrange
        var user = new User();

        // Act
        user.Role = UserRole.ServiceRep;

        // Assert
        user.Role.Should().Be(UserRole.ServiceRep);
    }

    [Fact]
    public void GivenARequesterUser_WhenTierSetToGold_ThenTierIsGold()
    {
        // Arrange
        var user = new User { Role = UserRole.Requester };

        // Act
        user.Tier = ServiceTier.Gold;

        // Assert
        user.Tier.Should().Be(ServiceTier.Gold);
    }

    [Fact]
    public void GivenRequesterRoleAndNoneTier_WhenValidateInvariantsCalled_ThenDomainExceptionIsThrown()
    {
        // Arrange
        var user = new User { Role = UserRole.Requester, Tier = ServiceTier.None };

        // Act
        var act = () => user.ValidateInvariants();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Requester*");
    }

    [Fact]
    public void GivenDispatcherRoleAndNoneTier_WhenValidateInvariantsCalled_ThenNoExceptionIsThrown()
    {
        // Arrange
        var user = new User { Role = UserRole.Dispatcher, Tier = ServiceTier.None };

        // Act
        var act = () => user.ValidateInvariants();

        // Assert
        act.Should().NotThrow();
    }
}
