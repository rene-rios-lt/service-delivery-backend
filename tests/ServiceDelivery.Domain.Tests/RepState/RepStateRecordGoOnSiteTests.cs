using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.RepState;

public class RepStateRecordGoOnSiteTests
{
    private static RepStateRecord RecordWithState(Domain.Enums.RepState state)
        => new()
        {
            RepId = Guid.NewGuid(),
            State = state,
            ActiveRequestId = Guid.NewGuid(),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    [Fact]
    public void GivenAWithin15MilesRep_WhenGoOnSite_ThenStateIsOnSite()
    {
        // Arrange
        var record = RecordWithState(Domain.Enums.RepState.Within15Miles);

        // Act
        record.GoOnSite();

        // Assert
        record.State.Should().Be(Domain.Enums.RepState.OnSite);
        record.UpdatedAt.Should().BeAfter(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(Domain.Enums.RepState.EnRoute)]
    [InlineData(Domain.Enums.RepState.OnSite)]
    [InlineData(Domain.Enums.RepState.Available)]
    [InlineData(Domain.Enums.RepState.Offline)]
    public void GivenARepNotWithin15Miles_WhenGoOnSite_ThenInvalidRepStateExceptionIsThrown(Domain.Enums.RepState state)
    {
        // Arrange
        var record = RecordWithState(state);

        // Act
        var act = () => record.GoOnSite();

        // Assert
        act.Should().Throw<InvalidRepStateException>();
        record.State.Should().Be(state);
    }
}
