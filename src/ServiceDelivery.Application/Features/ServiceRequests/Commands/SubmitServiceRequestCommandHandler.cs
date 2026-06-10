using MediatR;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.ServiceRequests.Commands;

public class SubmitServiceRequestCommandHandler : IRequestHandler<SubmitServiceRequestCommand, SubmitServiceRequestResult>
{
    private readonly IServiceRequestRepository _repository;
    private readonly IMatchingService _matchingService;

    public SubmitServiceRequestCommandHandler(
        IServiceRequestRepository repository,
        IMatchingService matchingService)
    {
        _repository = repository;
        _matchingService = matchingService;
    }

    public async Task<SubmitServiceRequestResult> Handle(SubmitServiceRequestCommand request, CancellationToken cancellationToken)
    {
        var serviceRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = request.RequesterId,
            DealerId = request.DealerId,
            Tier = request.Tier,
            DtcId = request.DtcId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Status = ServiceRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(serviceRequest, cancellationToken);
        await _matchingService.RunAsync(serviceRequest.Id, cancellationToken);

        return new SubmitServiceRequestResult(serviceRequest.Id, "Pending");
    }
}
