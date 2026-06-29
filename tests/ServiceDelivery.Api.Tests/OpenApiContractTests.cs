using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace ServiceDelivery.Api.Tests;

// ADR-0011 / QUAL-006: contracts/openapi.json is the committed source of truth for the
// REST wire contract. The build regenerates it (Microsoft.Extensions.ApiDescription.Server);
// this sync-check fails loudly if the committed copy drifts from what the app actually serves,
// so a stale contract is caught at test time rather than shipped to the consumers that mirror it.
public class OpenApiContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OpenApiContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GivenTheCommittedContract_WhenComparedToTheLiveOpenApiDocument_ThenTheyMatch()
    {
        // Arrange — MapOpenApi is Development-only, so serve the document under that environment.
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient();
        var committedPath = Path.Combine(AppContext.BaseDirectory, "contracts", "openapi.json");
        File.Exists(committedPath).Should().BeTrue(
            "the build generates contracts/openapi.json (ADR-0011) and it must be committed");

        // Act
        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var live = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(committedPath))!.AsObject();

        // The runtime adds a host-specific `servers` block (e.g. http://localhost:5180/); that is a
        // deployment detail, not part of the wire-contract shape, so normalise it out before comparing.
        live.Remove("servers");
        committed.Remove("servers");

        // Assert — structural equality, so formatting/whitespace differences do not matter.
        JsonNode.DeepEquals(live, committed).Should().BeTrue(
            "contracts/openapi.json is stale — rebuild ServiceDelivery.Api to regenerate it (ADR-0011), " +
            "then commit the updated contract so the frontend and simulator mirror the real shapes");
    }
}
