using MediatR;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.JobOffers.Commands;

public class DeclineJobOfferCommandHandler
    : IRequestHandler<DeclineJobOfferCommand, DeclineJobOfferResult>
{
    private readonly IJobOfferRepository _jobOfferRepository;
    private readonly IMatchingService _matchingService;

    public DeclineJobOfferCommandHandler(
        IJobOfferRepository jobOfferRepository,
        IMatchingService matchingService)
    {
        _jobOfferRepository = jobOfferRepository;
        _matchingService = matchingService;
    }

    public async Task<DeclineJobOfferResult> Handle(DeclineJobOfferCommand request, CancellationToken cancellationToken)
    {
        var offer = await _jobOfferRepository.GetByIdAsync(request.OfferId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job offer {request.OfferId} was not found.");

        offer.Decline();

        await _jobOfferRepository.UpdateAsync(offer, cancellationToken);

        await _matchingService.RunAsync(offer.ServiceRequestId, cancellationToken);

        return new DeclineJobOfferResult(
            offer.Id,
            offer.ServiceRequestId,
            offer.Status.ToString());
    }
}
