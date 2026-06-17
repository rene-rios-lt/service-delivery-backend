using MediatR;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Rep.Commands;

public class RepWentOfflineCommandHandler : IRequestHandler<RepWentOfflineCommand>
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IRepStateRepository _repStateRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDiagnosticTroubleCodeRepository _dtcRepository;
    private readonly IDispatchHubService _dispatchHub;
    private readonly IRequesterHubService _requesterHub;
    private readonly IMatchingService _matchingService;

    public RepWentOfflineCommandHandler(
        IServiceRequestRepository serviceRequestRepository,
        IRepStateRepository repStateRepository,
        IUserRepository userRepository,
        IDiagnosticTroubleCodeRepository dtcRepository,
        IDispatchHubService dispatchHub,
        IRequesterHubService requesterHub,
        IMatchingService matchingService)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _repStateRepository = repStateRepository;
        _userRepository = userRepository;
        _dtcRepository = dtcRepository;
        _dispatchHub = dispatchHub;
        _requesterHub = requesterHub;
        _matchingService = matchingService;
    }

    public async Task Handle(RepWentOfflineCommand request, CancellationToken cancellationToken)
    {
        var serviceRequest = await _serviceRequestRepository.GetActiveByRepIdAsync(request.RepId, cancellationToken);
        if (serviceRequest is null)
            return;

        var repState = await _repStateRepository.GetByRepIdAsync(request.RepId, cancellationToken);
        if (repState is null)
            return;

        serviceRequest.ReturnToPending();
        repState.GoOffline();

        await _serviceRequestRepository.UpdateAsync(serviceRequest, cancellationToken);
        await _repStateRepository.UpsertAsync(repState, cancellationToken);

        await BroadcastAsync(request.RepId, request.DealerId, serviceRequest, cancellationToken);

        await _matchingService.RunAsync(serviceRequest.Id, cancellationToken);
    }

    private async Task BroadcastAsync(
        Guid repId,
        Guid dealerId,
        Domain.Entities.ServiceRequest serviceRequest,
        CancellationToken cancellationToken)
    {
        var rep = await _userRepository.FindByIdAsync(repId, cancellationToken);
        var dtc = await _dtcRepository.GetByIdAsync(serviceRequest.DtcId, cancellationToken);

        await _dispatchHub.SendRepOfflineMidJobAsync(
            $"dealer:{dealerId}",
            new RepOfflineMidJobPayload(repId, serviceRequest.Id, rep?.Name ?? string.Empty, dtc?.HumanReadableTitle ?? string.Empty),
            cancellationToken);

        await _requesterHub.SendRequestBackToPendingAsync(
            $"requester:{serviceRequest.RequesterId}",
            new RequestBackToPendingPayload(serviceRequest.Id),
            cancellationToken);
    }
}
