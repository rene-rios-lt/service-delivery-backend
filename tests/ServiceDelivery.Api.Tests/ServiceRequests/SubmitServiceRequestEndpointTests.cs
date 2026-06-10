using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.ServiceRequests;

public class SubmitServiceRequestEndpointTests
{
    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    private static object ValidPostBody() => new
    {
        dtcId = SeedConstants.Dtc001Id,
        latitude = 37.7749,
        longitude = -122.4194
    };

    [Fact]
    public async Task GivenARequesterToken_WhenPostServiceRequests_ThenReturns200WithRequestIdAndStatus()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "bronze1@example.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/service-requests", ValidPostBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubmitServiceRequestResponse>();
        body.Should().NotBeNull();
        body!.RequestId.Should().NotBeEmpty();
        body.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task GivenARequesterToken_WhenPostServiceRequests_ThenResponseContainsNonEmptyRequestId()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "bronze1@example.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/service-requests", ValidPostBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubmitServiceRequestResponse>();
        body.Should().NotBeNull();
        body!.RequestId.Should().NotBeEmpty();
        body.RequestId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenPostServiceRequests_ThenReturns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/service-requests", ValidPostBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenAServiceRepToken_WhenPostServiceRequests_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/service-requests", ValidPostBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenPostServiceRequests_ThenReturns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/service-requests", ValidPostBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

internal record SubmitServiceRequestResponse(Guid RequestId, string Status);
