using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDelivery.Api.BackgroundServices;
using ServiceDelivery.Application.Common.Interfaces;

namespace ServiceDelivery.Api.Tests.Rep;

// AC-2 (Config): the heartbeat timeout binds from the "HeartbeatTimeout" configuration section —
// the timer cadence into HeartbeatTimeoutOptions (Api) and the staleness threshold into the
// Application-layer HeartbeatTimeoutSettings, both from the same section.
public class HeartbeatTimeoutOptionsConfigTests
{
    [Fact]
    public async Task GivenDefaultConfig_WhenAppStarts_ThenHeartbeatTimeoutOptionsBind()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        // Act
        var pollOptions = scope.ServiceProvider.GetRequiredService<IOptions<HeartbeatTimeoutOptions>>().Value;
        var settings = scope.ServiceProvider.GetRequiredService<HeartbeatTimeoutSettings>();

        // Assert
        pollOptions.PollIntervalSeconds.Should().Be(10);
        settings.TimeoutSeconds.Should().Be(45);
    }
}
