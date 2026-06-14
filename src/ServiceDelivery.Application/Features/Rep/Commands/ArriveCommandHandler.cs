using MediatR;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Exceptions;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Rep.Commands;

public class ArriveCommandHandler
    : IRequestHandler<ArriveCommand, ArriveResult>
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IRepStateRepository _repStateRepository;
    private readonly IDispatchHubService _dispatchHub;
    private readonly IRequesterHubService _requesterHub;

    public ArriveCommandHandler(
        IServiceRequestRepository serviceRequestRepository,
        IRepStateRepository repStateRepository,
        IDispatchHubService dispatchHub,
        IRequesterHubService requesterHub)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _repStateRepository = repStateRepository;
        _dispatchHub = dispatchHub;
        _requesterHub = requesterHub;
    }

    public async Task<ArriveResult> Handle(ArriveCommand request, CancellationToken cancellationToken)
    {
        var serviceRequest = await _serviceRequestRepository.GetActiveByRepIdAsync(request.RepId, cancellationToken)
            ?? throw new NoActiveAssignedRequestException(request.RepId);

        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken)
            ?? throw new NoActiveAssignedRequestException(request.RepId);

        var oldRepState = repState.State.ToString();

        repState.GoOnSite();
        serviceRequest.MarkInProgress();

        await _serviceRequestRepository.UpdateAsync(serviceRequest, cancellationToken);
        await _repStateRepository.UpsertAsync(repState, cancellationToken);

        await BroadcastAsync(request.RepId, serviceRequest, oldRepState, repState.State.ToString(), cancellationToken);

        return new ArriveResult(
            request.RepId,
            serviceRequest.Id,
            repState.State.ToString(),
            serviceRequest.Status.ToString());
    }

    private async Task BroadcastAsync(
        Guid repId,
        ServiceRequest serviceRequest,
        string oldRepState,
        string newRepState,
        CancellationToken cancellationToken)
    {
        await _dispatchHub.SendRepStateChangedAsync(
            $"dealer:{serviceRequest.DealerId}",
            new RepStateChangedPayload(repId, oldRepState, newRepState),
            cancellationToken);

        await _requesterHub.SendRepArrivedAsync(
            $"requester:{serviceRequest.RequesterId}",
            new RepArrivedPayload(repId, serviceRequest.Id),
            cancellationToken);
    }
}
