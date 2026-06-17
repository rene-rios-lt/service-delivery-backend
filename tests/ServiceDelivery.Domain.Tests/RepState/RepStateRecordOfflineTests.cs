using FluentAssertions;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Tests.RepState;

public class RepStateRecordOfflineTests
{
    [Fact]
    public void GivenARep_WhenGoOfflineCalled_ThenStateIsOfflineAndActiveRequestCleared()
    {
        // Arrange
        var record = new RepStateRecord
        {
            RepId = Guid.NewGuid(),
            State = Domain.Enums.RepState.EnRoute,
            ActiveRequestId = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        // Act
        record.GoOffline();

        // Assert
        record.State.Should().Be(Domain.Enums.RepState.Offline);
        record.ActiveRequestId.Should().BeNull();
    }

    [Fact]
    public void GivenAHumanControlledRep_WhenGoOfflineCalled_ThenHumanControlledIsFalse()
    {
        // Arrange
        var record = new RepStateRecord
        {
            RepId = Guid.NewGuid(),
            State = Domain.Enums.RepState.OnSite,
            ActiveRequestId = Guid.NewGuid(),
            HumanControlled = true,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        // Act
        record.GoOffline();

        // Assert
        record.HumanControlled.Should().BeFalse();
    }
}
