using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Auth;

public class AuthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GivenAValidCredential_WhenPostToAuthLogin_ThenReturns200WithToken()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new LoginCommand("alex@dealer.com", SeedConstants.DefaultPassword);

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        result!.Token.Should().NotBeNullOrEmpty();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Subject.Should().NotBeNullOrEmpty();
        jwt.Claims.Should().Contain(c => c.Type == "role");
        jwt.Claims.Should().Contain(c => c.Type == "tier");
        jwt.Claims.Should().Contain(c => c.Type == "dealerId");
    }

    [Fact]
    public async Task GivenAnInvalidCredential_WhenPostToAuthLogin_ThenReturns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new LoginCommand("alex@dealer.com", "WrongPassword!");

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenDefaultConfig_WhenTokenGenerated_ThenExpiryMatchesAppsettings()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new LoginCommand("alex@dealer.com", SeedConstants.DefaultPassword);

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", request);
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result!.Token);
        var expiry = token.ValidTo;

        var configuredMinutes = int.Parse(
            _factory.Services.GetRequiredService<IConfiguration>()["JwtSettings:ExpiryMinutes"]!);
        var expectedExpiry = DateTime.UtcNow.AddMinutes(configuredMinutes);

        expiry.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task GivenARequesterWithNoTier_WhenPostToAuthLogin_ThenReturns401()
    {
        // Arrange
        var tierlessRequester = new User
        {
            Id = Guid.NewGuid(),
            Email = "noTier@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedConstants.DefaultPassword),
            Role = UserRole.Requester,
            Tier = ServiceTier.None,
            DealerId = SeedConstants.DealerId
        };

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository
            .Setup(r => r.FindByEmailAsync("noTier@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tierlessRequester);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IUserRepository>(_ => mockUserRepository.Object);
            });
        }).CreateClient();

        var request = new LoginCommand("noTier@example.com", SeedConstants.DefaultPassword);

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
