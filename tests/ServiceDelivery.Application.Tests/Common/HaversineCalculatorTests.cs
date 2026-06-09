using FluentAssertions;
using ServiceDelivery.Application.Common;

namespace ServiceDelivery.Application.Tests.Common;

public class HaversineCalculatorTests
{
    // AC-4: ETA recalculated using Haversine at 60 mph
    [Fact]
    public void GivenAKnownDistance_WhenEtaCalculated_ThenEtaMatchesExpectedMinutes()
    {
        // Arrange
        var distanceMiles = 60.0;

        // Act
        var etaMinutes = HaversineCalculator.EtaMinutes(distanceMiles);

        // Assert
        etaMinutes.Should().BeApproximately(60.0, precision: 0.001);
    }

    [Fact]
    public void GivenIdenticalCoordinates_WhenDistanceCalculated_ThenDistanceIsZero()
    {
        // Arrange
        var lat = 40.7128;
        var lng = -74.0060;

        // Act
        var distance = HaversineCalculator.DistanceMiles(lat, lng, lat, lng);

        // Assert
        distance.Should().BeApproximately(0.0, precision: 0.001);
    }

    [Fact]
    public void GivenAKnownCoordinatePair_WhenDistanceCalculated_ThenDistanceMatchesExpectedMiles()
    {
        // Arrange — New York City to Philadelphia (approx 94 miles)
        var lat1 = 40.7128;
        var lng1 = -74.0060;
        var lat2 = 39.9526;
        var lng2 = -75.1652;

        // Act
        var distance = HaversineCalculator.DistanceMiles(lat1, lng1, lat2, lng2);

        // Assert — expected ~80.5 miles straight-line Haversine, tolerance ±2 miles
        distance.Should().BeInRange(78.5, 82.5);
    }

    [Fact]
    public void GivenThirtyMileDistance_WhenEtaCalculated_ThenEtaIsThirtyMinutes()
    {
        // Arrange
        var distanceMiles = 30.0;

        // Act
        var etaMinutes = HaversineCalculator.EtaMinutes(distanceMiles);

        // Assert — 30 miles at 60 mph = 30 minutes
        etaMinutes.Should().BeApproximately(30.0, precision: 0.001);
    }
}
