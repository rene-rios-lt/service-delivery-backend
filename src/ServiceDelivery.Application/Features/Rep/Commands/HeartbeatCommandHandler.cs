using MediatR;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Rep.Commands;

public class HeartbeatCommandHandler : IRequestHandler<HeartbeatCommand, HeartbeatResult>
{
    private readonly IRepStateRepository _repStateRepository;

    public HeartbeatCommandHandler(IRepStateRepository repStateRepository)
    {
        _repStateRepository = repStateRepository;
    }

    public async Task<HeartbeatResult> Handle(HeartbeatCommand request, CancellationToken cancellationToken)
    {
        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken)
            ?? throw new KeyNotFoundException($"Rep {request.RepId} has no state record to record a heartbeat against.");

        repState.RecordHeartbeat();

        await _repStateRepository.UpsertAsync(repState, cancellationToken);

        return new HeartbeatResult(request.RepId, repState.LastHeartbeatAt!.Value);
    }
}
