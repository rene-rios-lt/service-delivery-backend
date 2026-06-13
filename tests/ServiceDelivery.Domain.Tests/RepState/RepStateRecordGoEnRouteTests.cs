using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Tests.RepState;

public class RepStateRecordGoEnRouteTests
{
    [Fact]
    public void GivenARepStateRecord_WhenGoEnRoute_ThenStateIsEnRouteWithActiveRequest()
    {
        // Arrange
        var record = new RepStateRecord
        {
            RepId = Guid.NewGuid(),
            State = Domain.Enums.RepState.Available,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var requestId = Guid.NewGuid();

        // Act
        record.GoEnRoute(requestId);

        // Assert
        record.State.Should().Be(Domain.Enums.RepState.EnRoute);
        record.ActiveRequestId.Should().Be(requestId);
        record.UpdatedAt.Should().BeAfter(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
