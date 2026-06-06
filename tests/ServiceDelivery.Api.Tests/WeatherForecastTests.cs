using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ServiceDelivery.Api.Tests;

public class WeatherForecastTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetWeatherForecast_Returns200()
    {
        var response = await _client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsFiveItems()
    {
        var forecasts = await _client.GetFromJsonAsync<WeatherForecastResponse[]>("/weatherforecast");

        Assert.NotNull(forecasts);
        Assert.Equal(5, forecasts.Length);
    }

    [Fact]
    public async Task GetWeatherForecast_EachItemHasValidTemperature()
    {
        var forecasts = await _client.GetFromJsonAsync<WeatherForecastResponse[]>("/weatherforecast");

        Assert.NotNull(forecasts);
        Assert.All(forecasts, f =>
        {
            Assert.InRange(f.TemperatureC, -20, 55);
            Assert.NotNull(f.Summary);
        });
    }

    [Fact]
    public async Task GetWeatherForecast_TemperatureFIsCorrectlyConverted()
    {
        var forecasts = await _client.GetFromJsonAsync<WeatherForecastResponse[]>("/weatherforecast");

        Assert.NotNull(forecasts);
        Assert.All(forecasts, f =>
        {
            var expectedF = 32 + (int)(f.TemperatureC / 0.5556);
            Assert.Equal(expectedF, f.TemperatureF);
        });
    }

    private record WeatherForecastResponse(
        DateOnly Date,
        int TemperatureC,
        int TemperatureF,
        string? Summary);
}
