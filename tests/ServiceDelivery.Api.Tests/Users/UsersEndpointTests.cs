using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.Users.Queries;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Users;

public class UsersEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UsersEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    [Fact]
    public async Task GivenAnAuthenticatedUserToken_WhenGetToUsersMe_ThenReturns200WithProfileFields()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResult>();
        profile!.UserId.Should().Be(SeedConstants.AlexDispatcherId);
        profile.Name.Should().NotBeNullOrEmpty();
        profile.Role.Should().Be(UserRole.Dispatcher);
        profile.DealerId.Should().Be(SeedConstants.DealerId);
    }

    [Fact]
    public async Task GivenATokenWithSubClaim_WhenGetToUsersMe_ThenReturnedUserIdMatchesTokenSub()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResult>();
        profile!.UserId.Should().Be(SeedConstants.AlexDispatcherId);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetToUsersMe_ThenReturns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
