using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Application.Features.Dtcs.Queries;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.Dtcs;

public class DtcsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DtcsEndpointTests(CustomWebApplicationFactory factory)
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
    public async Task GivenAServiceRep_WhenGetDtcsCalled_ThenReturns10DtcsForDealer()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/dtcs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtcs = await response.Content.ReadFromJsonAsync<DtcDto[]>();
        dtcs.Should().HaveCount(10);
    }

    [Fact]
    public async Task GivenARequester_WhenGetDtcsCalled_ThenReturns10DtcsForDealer()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "bronze1@example.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/dtcs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtcs = await response.Content.ReadFromJsonAsync<DtcDto[]>();
        dtcs.Should().HaveCount(10);
    }

    [Fact]
    public async Task GivenAServiceRep_WhenGetDtcsCalled_ThenEachDtcHasIdCodeTitleAndRequiredEquipment()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "rep1@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/dtcs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtcs = await response.Content.ReadFromJsonAsync<DtcDto[]>();
        dtcs.Should().NotBeEmpty();

        var spotCheck = dtcs!.FirstOrDefault(d => d.Id == SeedConstants.Dtc001Id);
        spotCheck.Should().NotBeNull();
        spotCheck!.Id.Should().Be(SeedConstants.Dtc001Id);
        spotCheck.Code.Should().Be("DTC-001");
        spotCheck.Title.Should().Be("Hydraulic system fault");
        spotCheck.RequiredEquipment.Should().Be("HydraulicTool");
    }

    [Fact]
    public async Task GivenADispatcherToken_WhenGetDtcsCalled_ThenReturns403()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "alex@dealer.com", SeedConstants.DefaultPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/dtcs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GivenNoAuthorizationHeader_WhenGetDtcsCalled_ThenReturns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/dtcs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
