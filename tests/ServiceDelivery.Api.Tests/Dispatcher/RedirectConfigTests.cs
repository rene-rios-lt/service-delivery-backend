using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Application.Common.Interfaces;

namespace ServiceDelivery.Api.Tests.Dispatcher;

public class RedirectConfigTests
{
    [Fact]
    public void GivenDefaultConfig_WhenAppStarts_ThenRedirectCooldownMinutesIsConfigured()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();

        // Act
        var options = factory.Services.GetRequiredService<RedirectOptions>();

        // Assert
        options.CooldownMinutes.Should().Be(5);
    }
}
