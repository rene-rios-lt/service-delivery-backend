using FluentAssertions;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Tests.RepState;

public class RepStateRecordIsOnActiveJobTests
{
    private static RepStateRecord RecordWithState(Domain.Enums.RepState state)
        => new()
        {
            RepId = Guid.NewGuid(),
            State = state,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    [Theory]
    [InlineData(Domain.Enums.RepState.EnRoute)]
    [InlineData(Domain.Enums.RepState.Within15Miles)]
    [InlineData(Domain.Enums.RepState.OnSite)]
    public void GivenARepInAnEnRouteOrArrivingOrOnSiteState_WhenIsOnActiveJob_ThenReturnsTrue(
        Domain.Enums.RepState state)
    {
        // Arrange
        var record = RecordWithState(state);

        // Act
        var result = record.IsOnActiveJob();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(Domain.Enums.RepState.Available)]
    [InlineData(Domain.Enums.RepState.Offline)]
    public void GivenAnIdleRep_WhenIsOnActiveJob_ThenReturnsFalse(Domain.Enums.RepState state)
    {
        // Arrange
        var record = RecordWithState(state);

        // Act
        var result = record.IsOnActiveJob();

        // Assert
        result.Should().BeFalse();
    }
}
