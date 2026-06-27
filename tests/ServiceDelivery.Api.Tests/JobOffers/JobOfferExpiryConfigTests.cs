using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDelivery.Api.BackgroundServices;
using ServiceDelivery.Application.Common;

namespace ServiceDelivery.Api.Tests.JobOffers;

[Collection("Hub Tests")]
public class JobOfferExpiryConfigTests
{
    [Fact]
    public void GivenDefaultConfig_WhenAppStarts_ThenJobOfferExpiryPollIntervalIsConfigured()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        // Act
        var options = scope.ServiceProvider.GetRequiredService<IOptions<JobOfferExpiryOptions>>().Value;

        // Assert
        options.PollIntervalSeconds.Should().Be(10);
    }

    [Fact]
    public void GivenDefaultConfig_WhenAppStarts_ThenOfferExpirySecondsDefaultsTo60()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        // Act
        var options = scope.ServiceProvider.GetRequiredService<MatchingOptions>();

        // Assert
        options.OfferExpirySeconds.Should().Be(60);
    }

    [Fact]
    public void GivenTheHostedService_WhenResolved_ThenExpireJobOffersBackgroundServiceIsRegistered()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();

        // Act
        var hostedServices = factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>();

        // Assert
        hostedServices.Should().ContainSingle(s => s is ExpireJobOffersBackgroundService);
    }
}
