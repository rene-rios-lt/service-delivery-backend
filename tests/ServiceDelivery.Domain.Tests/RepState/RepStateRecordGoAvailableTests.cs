using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Exceptions;

namespace ServiceDelivery.Domain.Tests.RepState;

public class RepStateRecordGoAvailableTests
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
    public void GivenAnOnSiteRep_WhenGoAvailable_ThenStateIsAvailableAndActiveRequestCleared()
    {
        // Arrange
        var record = RecordWithState(Domain.Enums.RepState.OnSite);

        // Act
        record.GoAvailable();

        // Assert
        record.State.Should().Be(Domain.Enums.RepState.Available);
        record.ActiveRequestId.Should().BeNull();
        record.UpdatedAt.Should().BeAfter(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(Domain.Enums.RepState.EnRoute)]
    [InlineData(Domain.Enums.RepState.Within15Miles)]
    [InlineData(Domain.Enums.RepState.Available)]
    [InlineData(Domain.Enums.RepState.Offline)]
    public void GivenARepNotOnSite_WhenGoAvailable_ThenInvalidRepStateExceptionIsThrown(Domain.Enums.RepState state)
    {
        // Arrange
        var record = RecordWithState(state);

        // Act
        var act = () => record.GoAvailable();

        // Assert
        act.Should().Throw<InvalidRepStateException>();
        record.State.Should().Be(state);
    }
}
