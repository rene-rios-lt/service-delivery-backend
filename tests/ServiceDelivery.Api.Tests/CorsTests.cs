using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests;

public class CorsTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string AllowedOrigin = "http://localhost:5023";
    private const string UnlistedOrigin = "http://localhost:9999";

    private readonly CustomWebApplicationFactory _factory;

    public CorsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GivenAllowedOrigin_WhenPostAuthLogin_ThenCorsOriginHeaderIsReturned()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(
                new LoginCommand("alex@dealer.com", SeedConstants.DefaultPassword))
        };
        request.Headers.Add("Origin", AllowedOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be(AllowedOrigin);
    }

    [Fact]
    public async Task GivenAllowedOriginAndValidCredentials_WhenPostAuthLogin_ThenLoginSucceeds()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(
                new LoginCommand("alex@dealer.com", SeedConstants.DefaultPassword))
        };
        request.Headers.Add("Origin", AllowedOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        result!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GivenAllowedOrigin_WhenOptionsPreflightToHub_ThenCorsAllowCredentialsHeaderIsReturned()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/rep");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be(AllowedOrigin);
        response.Headers.Should().ContainKey("Access-Control-Allow-Credentials");
        response.Headers.GetValues("Access-Control-Allow-Credentials")
            .Should().ContainSingle().Which.Should().Be("true");
    }

    [Fact]
    public async Task GivenUnlistedOrigin_WhenPostAuthLogin_ThenNoCorsOriginHeaderIsReturned()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(
                new LoginCommand("alex@dealer.com", SeedConstants.DefaultPassword))
        };
        request.Headers.Add("Origin", UnlistedOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Should().NotContainKey("Access-Control-Allow-Origin");
    }
}
