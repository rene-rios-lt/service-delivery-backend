using MediatR;
using Microsoft.Extensions.Logging;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Features.Rep.Commands;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Common.Services;

// AC-2: finds human-controlled reps whose heartbeat has gone stale and takes them offline.
// SRP boundary: this class only DISCOVERS stale reps and DISPATCHES the offline action. The re-queue
// orchestration (return request to Pending, re-match, broadcasts) is owned by RepWentOfflineCommand,
// which we delegate to for the with-job case. The no-job case is handled directly here because the
// command handler returns early when there is no active job, so delegating it would be a silent no-op.
public class StaleHeartbeatSweeper : IStaleHeartbeatSweeper
{
    private readonly IRepStateRepository _repStates;
    private readonly IUserRepository _users;
    private readonly IMediator _mediator;
    private readonly HeartbeatTimeoutSettings _settings;
    private readonly ILogger<StaleHeartbeatSweeper> _logger;

    public StaleHeartbeatSweeper(
        IRepStateRepository repStates,
        IUserRepository users,
        IMediator mediator,
        HeartbeatTimeoutSettings settings,
        ILogger<StaleHeartbeatSweeper> logger)
    {
        _repStates = repStates;
        _users = users;
        _mediator = mediator;
        _settings = settings;
        _logger = logger;
    }

    public async Task SweepAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        var cutoff = asOf.UtcDateTime.AddSeconds(-_settings.TimeoutSeconds);
        var staleReps = await _repStates.GetStaleHumanControlledAsync(cutoff, cancellationToken);

        foreach (var repState in staleReps)
            await TakeOfflineAsync(repState, cancellationToken);
    }

    private async Task TakeOfflineAsync(RepStateRecord repState, CancellationToken cancellationToken)
    {
        // One stale rep failing must never abort the sweep for the rest.
        try
        {
            if (repState.ActiveRequestId is null)
                await TakeOfflineDirectlyAsync(repState, cancellationToken);
            else
                await DelegateReQueueAsync(repState, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to take stale human-controlled rep {RepId} offline; will retry on the next sweep.",
                repState.RepId);
        }
    }

    private async Task TakeOfflineDirectlyAsync(RepStateRecord repState, CancellationToken cancellationToken)
    {
        repState.GoOffline();
        await _repStates.UpsertAsync(repState, cancellationToken);
    }

    private async Task DelegateReQueueAsync(RepStateRecord repState, CancellationToken cancellationToken)
    {
        var rep = await _users.FindByIdAsync(repState.RepId, cancellationToken);
        if (rep is null)
        {
            _logger.LogWarning(
                "Stale rep {RepId} has an active job but no user record; cannot resolve dealer to re-queue.",
                repState.RepId);
            return;
        }

        await _mediator.Send(new RepWentOfflineCommand(repState.RepId, rep.DealerId), cancellationToken);
    }
}
