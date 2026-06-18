using FluentAssertions;
using ServiceDelivery.Domain.Entities;

namespace ServiceDelivery.Domain.Tests.RepState;

public class RepStateRecordRedirectTests
{
    private static readonly DateTime Anchor = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RepStateRecord EnRouteRecord(Guid activeRequestId)
        => new()
        {
            RepId = Guid.NewGuid(),
            State = Domain.Enums.RepState.EnRoute,
            ActiveRequestId = activeRequestId,
            UpdatedAt = Anchor
        };

    [Fact]
    public void GivenAnEnRouteRep_WhenRedirect_ThenActiveRequestSwappedAndStatePreserved()
    {
        // Arrange
        var record = EnRouteRecord(Guid.NewGuid());
        var newRequestId = Guid.NewGuid();
        var now = Anchor.AddMinutes(3);

        // Act
        record.Redirect(newRequestId, now);

        // Assert
        record.State.Should().Be(Domain.Enums.RepState.EnRoute);
        record.ActiveRequestId.Should().Be(newRequestId);
    }

    [Fact]
    public void GivenAnEnRouteRep_WhenRedirect_ThenLastRedirectedAtAndUpdatedAtAreStamped()
    {
        // Arrange
        var record = EnRouteRecord(Guid.NewGuid());
        var now = Anchor.AddMinutes(3);

        // Act
        record.Redirect(Guid.NewGuid(), now);

        // Assert
        record.LastRedirectedAt.Should().Be(now);
        record.UpdatedAt.Should().Be(now);
    }
}
