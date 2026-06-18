namespace ServiceDelivery.Application.Features.Rep.Commands;

public record HeartbeatResult(Guid RepId, DateTime LastHeartbeatAt);
