using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Moq;
using ServiceDelivery.Api.Hubs;
using ServiceDelivery.Application.Features.Rep.Commands;

namespace ServiceDelivery.Api.Tests.Hubs;

public class RepHubDisconnectTests
{
    private static RepHub BuildHub(Mock<IMediator> mediator, Guid repId, Guid dealerId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, repId.ToString()),
            new Claim("dealerId", dealerId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.User).Returns(principal);
        context.SetupGet(c => c.ConnectionId).Returns("conn-1");

        return new RepHub(mediator.Object)
        {
            Context = context.Object
        };
    }

    [Fact]
    public async Task GivenAConnectedRep_WhenDisconnected_ThenRepWentOfflineCommandIsSent()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        var hub = BuildHub(mediator, repId, dealerId);

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        mediator.Verify(m => m.Send(
            It.Is<RepWentOfflineCommand>(c => c.RepId == repId && c.DealerId == dealerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
