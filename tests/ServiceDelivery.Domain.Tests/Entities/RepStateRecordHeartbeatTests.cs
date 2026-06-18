using FluentAssertions;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Tests.Entities;

public class RepStateRecordHeartbeatTests
{
    [Fact]
    public void GivenARepStateRecord_WhenRecordHeartbeatCalled_ThenLastHeartbeatAtAndUpdatedAtAreSet()
    {
        // Arrange
        var record = new RepStateRecord
        {
            RepId = Guid.NewGuid(),
            State = Domain.Enums.RepState.Available,
            HumanControlled = true,
            LastHeartbeatAt = null,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var before = DateTime.UtcNow;

        // Act
        record.RecordHeartbeat();

        // Assert
        record.LastHeartbeatAt.Should().NotBeNull();
        record.LastHeartbeatAt!.Value.Should().BeOnOrAfter(before);
        record.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void GivenAHumanControlledRepStateRecord_WhenGoOfflineCalled_ThenHumanControlledIsFalse()
    {
        // Arrange
        var record = new RepStateRecord
        {
            RepId = Guid.NewGuid(),
            State = Domain.Enums.RepState.Available,
            HumanControlled = true,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        record.GoOffline();

        // Assert
        record.HumanControlled.Should().BeFalse();
        record.State.Should().Be(Domain.Enums.RepState.Offline);
    }
}
