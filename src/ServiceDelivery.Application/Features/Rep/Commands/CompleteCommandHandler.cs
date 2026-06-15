using MediatR;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Rep.Commands;

public class CompleteCommandHandler
    : IRequestHandler<CompleteCommand, CompleteResult>
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IRepStateRepository _repStateRepository;
    private readonly IDispatchHubService _dispatchHub;
    private readonly IRequesterHubService _requesterHub;
    private readonly IMatchingService _matchingService;

    public CompleteCommandHandler(
        IServiceRequestRepository serviceRequestRepository,
        IRepStateRepository repStateRepository,
        IDispatchHubService dispatchHub,
        IRequesterHubService requesterHub,
        IMatchingService matchingService)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _repStateRepository = repStateRepository;
        _dispatchHub = dispatchHub;
        _requesterHub = requesterHub;
        _matchingService = matchingService;
    }

    public async Task<CompleteResult> Handle(CompleteCommand request, CancellationToken cancellationToken)
    {
        var serviceRequest = await _serviceRequestRepository.GetActiveByRepIdAsync(request.RepId, cancellationToken)
            ?? throw new NoActiveAssignedRequestException(request.RepId);

        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken)
            ?? throw new NoActiveAssignedRequestException(request.RepId);

        var oldRepState = repState.State.ToString();

        serviceRequest.MarkCompleted();
        repState.GoAvailable();

        await _serviceRequestRepository.UpdateAsync(serviceRequest, cancellationToken);
        await _repStateRepository.UpsertAsync(repState, cancellationToken);

        await BroadcastAsync(request.RepId, serviceRequest, oldRepState, repState.State.ToString(), cancellationToken);

        // The rep is now Available again — re-run matching so any Pending requests
        // for this dealer can be offered to the freed-up rep.
        await _matchingService.RunForPendingByDealerAsync(serviceRequest.DealerId, cancellationToken);

        return new CompleteResult(
            request.RepId,
            serviceRequest.Id,
            repState.State.ToString(),
            serviceRequest.Status.ToString());
    }

    private async Task BroadcastAsync(
        Guid repId,
        Domain.Entities.ServiceRequest serviceRequest,
        string oldRepState,
        string newRepState,
        CancellationToken cancellationToken)
    {
        await _requesterHub.SendServiceCompletedAsync(
            $"requester:{serviceRequest.RequesterId}",
            new ServiceCompletedPayload(serviceRequest.Id),
            cancellationToken);

        await _dispatchHub.SendRepStateChangedAsync(
            $"dealer:{serviceRequest.DealerId}",
            new RepStateChangedPayload(repId, oldRepState, newRepState),
            cancellationToken);

        await _dispatchHub.SendServiceRequestCompletedAsync(
            $"dealer:{serviceRequest.DealerId}",
            new ServiceRequestCompletedPayload(serviceRequest.Id),
            cancellationToken);
    }
}
