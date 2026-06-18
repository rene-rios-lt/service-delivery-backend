using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDelivery.Api.BackgroundServices;

namespace ServiceDelivery.Api.Tests.PendingRequests;

[Collection("Hub Tests")]
public class ReconcilePendingRequestsConfigTests
{
    [Fact]
    public void GivenDefaultConfig_WhenAppStarts_ThenReconcilePendingRequestsPollIntervalIsConfigured()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        // Act
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<ReconcilePendingRequestsOptions>>().Value;

        // Assert
        options.PollIntervalSeconds.Should().Be(30);
    }

    [Fact]
    public void GivenTheHostedService_WhenResolved_ThenReconcilePendingRequestsBackgroundServiceIsRegistered()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();

        // Act
        var hostedServices = factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>();

        // Assert
        hostedServices.Should().ContainSingle(s => s is ReconcilePendingRequestsBackgroundService);
    }
}
