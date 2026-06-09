using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using ServiceDelivery.Application.Features.Auth.Commands;

namespace ServiceDelivery.Api.Tests.Hubs;

public static class HubTestHelpers
{
    public static async Task<string> GetTokenAsync(CustomWebApplicationFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        return loginResult!.Token;
    }

    public static HubConnection BuildHubConnection(CustomWebApplicationFactory factory, string hubPath, string? bearerToken = null)
    {
        var client = factory.CreateClient();
        var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');

        var builder = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}{hubPath}?access_token={bearerToken ?? string.Empty}", opts =>
            {
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            });

        return builder.Build();
    }
}
