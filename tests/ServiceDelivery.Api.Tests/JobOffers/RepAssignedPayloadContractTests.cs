using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Api.Tests.Hubs;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;

namespace ServiceDelivery.Api.Tests.JobOffers;

// Captured-payload / wire-contract test guarding ADR-0011 for the RepAssigned SignalR event.
// It asserts the raw JSON field name and casing on the live event (not just the typed payload),
// because the frontend and simulator mirror these wire shapes. The typed re-deserialisation
// verifies the value round-trips correctly.
[Collection("Hub Tests")]
public class RepAssignedPayloadContractTests
{
    private static async Task<Guid> SeedOfferAsync(CustomWebApplicationFactory factory, Guid offerId, Guid repId)
    {
        var requestId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.ServiceRequests.Add(new ServiceRequest
        {
            Id = requestId,
            DealerId = SeedConstants.DealerId,
            RequesterId = SeedConstants.Bronze1Id,
            DtcId = SeedConstants.Dtc001Id,
            Tier = ServiceTier.Gold,
            Status = ServiceRequestStatus.Pending,
            Latitude = 41.6,
            Longitude = -93.6,
            CreatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        db.JobOffers.Add(new JobOffer
        {
            Id = offerId,
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            Status = JobOfferStatus.Pending
        });

        db.RepStateRecords.Add(new RepStateRecord
        {
            RepId = repId,
            State = RepState.Available,
            UpdatedAt = new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc)
        });

        var vehicle = await db.Vehicles.FindAsync(SeedConstants.Vehicle1Id);
        if (vehicle is not null)
        {
            vehicle.ClaimedByRepId = repId;
            vehicle.LastLatitude = 41.5;
            vehicle.LastLongitude = -93.6;
        }

        await db.SaveChangesAsync();
        return requestId;
    }

    private static async Task AcceptAsRepAsync(CustomWebApplicationFactory factory, Guid offerId)
    {
        var repToken = await HubTestHelpers.GetTokenAsync(factory, "rep1@dealer.com", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);
        await client.PostAsync($"/job-offers/{offerId}/accept", null);
    }

    // AC-1 / AC-5: the RepAssigned wire payload carries a camelCase "vehicleRegistration" field,
    // populated from the accepting rep's claimed vehicle registration.
    [Fact]
    public async Task GivenARepAssignedPayload_WhenSerialised_ThenVehicleRegistrationFieldIsCamelCase()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var offerId = Guid.NewGuid();
        await SeedOfferAsync(factory, offerId, SeedConstants.Rep1Id);

        var requesterToken = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<JsonElement>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requesterToken);
        connection.On<JsonElement>("RepAssigned", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        // Act
        await AcceptAsRepAsync(factory, offerId);

        // Assert
        var raw = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        raw.TryGetProperty("vehicleRegistration", out var registration).Should()
            .BeTrue("the RepAssigned wire payload must expose a camelCase 'vehicleRegistration' field (ADR-0011)");
        registration.GetString().Should().Be("V-001");

        var typed = raw.Deserialize<RepAssignedPayload>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        typed!.VehicleRegistration.Should().Be("V-001");

        await connection.StopAsync();
    }

    // AC-6: RepPositionUpdated is unchanged by this story — its wire payload must NOT carry a
    // vehicleRegistration field (read-only enrichment applies only to RepAssigned).
    [Fact]
    public async Task GivenAConnectedRequester_WhenVehiclePositionIsUpdated_ThenRepPositionUpdatedDoesNotContainVehicleRegistration()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RepStateRecords.Add(new RepStateRecord
            {
                RepId = SeedConstants.Rep1Id,
                State = RepState.EnRoute,
                UpdatedAt = DateTime.UtcNow
            });
            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = Guid.NewGuid(),
                DealerId = SeedConstants.DealerId,
                RequesterId = SeedConstants.Bronze1Id,
                DtcId = SeedConstants.Dtc001Id,
                Latitude = 51.5074,
                Longitude = -0.1278,
                Status = ServiceRequestStatus.Assigned,
                AssignedRepId = SeedConstants.Rep1Id,
                CreatedAt = DateTime.UtcNow
            });
            var vehicle = db.Vehicles.First(v => v.Id == SeedConstants.Vehicle1Id);
            vehicle.ClaimedByRepId = SeedConstants.Rep1Id;
            await db.SaveChangesAsync();
        }

        var requesterToken = await HubTestHelpers.GetTokenAsync(factory, "bronze1@example.com", SeedConstants.DefaultPassword);
        var tcs = new TaskCompletionSource<JsonElement>();
        var connection = HubTestHelpers.BuildHubConnection(factory, "/hubs/requester", requesterToken);
        connection.On<JsonElement>("RepPositionUpdated", tcs.SetResult);
        await connection.StartAsync();
        await HubTestHelpers.WaitForReadyAsync(connection);

        var simulatorToken = await HubTestHelpers.GetTokenAsync(factory, "simulator@system.internal", SeedConstants.DefaultPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", simulatorToken);

        var body = new { latitude = 51.5074, longitude = -0.1278, timestamp = DateTime.UtcNow };

        // Act
        await client.PostAsJsonAsync($"/vehicles/{SeedConstants.Vehicle1Id}/position", body);

        // Assert
        var raw = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        raw.TryGetProperty("vehicleRegistration", out _).Should()
            .BeFalse("RepPositionUpdated is unchanged by this story and must not carry vehicleRegistration");

        await connection.StopAsync();
    }
}
