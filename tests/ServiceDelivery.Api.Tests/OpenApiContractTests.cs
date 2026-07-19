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

    // QUAL-013: every REST success response must carry a typed schema so the committed contract
    // guards response shapes (not just request bodies). Adding [ProducesResponseType<T>] to each
    // action makes the OpenAPI generator emit a `content` block per 2xx response; this test fails
    // if any 2xx response is untyped, and — via the sync-check above — turns the class of change
    // BE-032 slipped through (a response DTO field that produced no contract diff) into a real diff.
    // Purely file-based: it inspects the committed artifact, not the live document.
    [Fact]
    public async Task GivenTheCommittedContract_WhenInspected_ThenEvery2xxResponseHasASchema()
    {
        // Arrange
        var committedPath = Path.Combine(AppContext.BaseDirectory, "contracts", "openapi.json");
        File.Exists(committedPath).Should().BeTrue(
            "contracts/openapi.json must be present — check the test project's Content link");

        // Act
        var doc = JsonNode.Parse(await File.ReadAllTextAsync(committedPath))!.AsObject();
        var paths = doc["paths"]!.AsObject();

        var unschematized = new List<string>();
        foreach (var (path, pathItem) in paths)
        {
            foreach (var (method, operation) in pathItem!.AsObject())
            {
                var responses = operation!.AsObject()["responses"]?.AsObject();
                if (responses is null) continue;
                foreach (var (statusCode, response) in responses)
                {
                    if (!statusCode.StartsWith("2")) continue;
                    var hasContent = response?.AsObject().ContainsKey("content") ?? false;
                    if (!hasContent)
                        unschematized.Add($"{method.ToUpperInvariant()} {path} {statusCode}");
                }
            }
        }

        // Assert
        unschematized.Should().BeEmpty(
            "every 2xx response must have a schema — add [ProducesResponseType<T>] to each " +
            "action and run ./scripts/regen-openapi.sh (AC-2/AC-3, QUAL-013)");
    }
}
