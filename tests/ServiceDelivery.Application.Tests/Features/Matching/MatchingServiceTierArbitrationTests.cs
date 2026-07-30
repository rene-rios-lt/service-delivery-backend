using Moq;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Services;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Application.Tests.Features.Matching;

// BUG-064: these tests pin the free-up interpretation of tier arbitration — multiple requests are
// already Pending and compete for a rep that has just freed up, and MatchingService serves them via
// RunForPendingByDealerAsync. The repository (see ServiceRequestRepositoryTierOrderingTests) returns
// the dealer's Pending set in tier-desc, CreatedAt-asc order; these tests assert MatchingService
// honours that order so the highest-tier, then oldest, request wins the freed-up rep. The submit-time
// single-request RunAsync path is deliberately unchanged (FCFS; preemption is the manual dispatcher
// redirect) and is not exercised here.
public class MatchingServiceTierArbitrationTests
{
    private readonly Mock<IServiceRequestRepository> _requests = new();
    private readonly Mock<IDiagnosticTroubleCodeRepository> _dtcs = new();
    private readonly Mock<IRepStateRepository> _repStates = new();
    private readonly Mock<IJobOfferRepository> _jobOffers = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();

    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid DtcId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();

    private MatchingService CreateService(int offerExpirySeconds = 60) => new(
        _requests.Object,
        _dtcs.Object,
        _repStates.Object,
        _jobOffers.Object,
        _users.Object,
        _repHub.Object,
        _dispatchHub.Object,
        new MatchingOptions { OfferExpirySeconds = offerExpirySeconds });

    private ServiceRequest BuildRequest(ServiceTier tier, DateTime createdAt, double lat = 10.0, double lng = 10.0)
        => new()
        {
            Id = Guid.NewGuid(),
            DealerId = DealerId,
            RequesterId = RequesterId,
            DtcId = DtcId,
            Latitude = lat,
            Longitude = lng,
            Status = ServiceRequestStatus.Pending,
            Tier = tier,
            CreatedAt = createdAt
        };

    private void ArrangeRequest(ServiceRequest request)
    {
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _jobOffers.Setup(j => j.GetSkippedRepIdsForRequestAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        _jobOffers.Setup(j => j.GetLivePendingOfferForRequestAsync(
                request.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobOffer?)null);
    }

    private void ArrangePendingByDealer(params ServiceRequest[] ordered)
        => _requests.Setup(r => r.GetPendingByDealerAsync(DealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordered);

    private void ArrangeDtc()
        => _dtcs.Setup(d => d.GetByIdAsync(DtcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticTroubleCode
            {
                Id = DtcId,
                DealerId = DealerId,
                Code = "DTC-001",
                HumanReadableTitle = "Hydraulic system fault",
                RequiredEquipmentType = EquipmentType.HydraulicTool
            });

    private void ArrangeRequester()
        => _users.Setup(u => u.FindByIdAsync(RequesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = RequesterId,
                Name = "Requester",
                Email = "requester@example.com",
                PasswordHash = "x",
                Role = UserRole.Requester,
                Tier = ServiceTier.Gold,
                DealerId = DealerId
            });

    private void ArrangeCandidates(params RepMatchCandidate[] candidates)
        => _repStates.Setup(r => r.GetAvailableByDealerAsync(DealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

    private void ArrangeBusyRepSequence(params Guid[][] busyRepIdsPerCall)
    {
        var sequence = _jobOffers.SetupSequence(j => j.GetRepIdsWithLivePendingOfferAsync(
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()));
        foreach (var busyRepIds in busyRepIdsPerCall)
            sequence = sequence.ReturnsAsync((IReadOnlyList<Guid>)busyRepIds);
    }

    private static RepMatchCandidate Candidate(Guid repId, double lat, double lng, DateTime availableSince)
        => new(repId, lat, lng, new[] { EquipmentType.HydraulicTool }, availableSince);

    [Fact]
    public async Task GivenGoldAndSilverPendingRequestsEligibleForSameRep_WhenRunForPendingByDealer_ThenGoldIsOfferedFirst()
    {
        // Arrange
        var gold = BuildRequest(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-1));
        var silver = BuildRequest(ServiceTier.Silver, DateTime.UtcNow.AddMinutes(-2));
        ArrangeRequest(gold);
        ArrangeRequest(silver);
        ArrangePendingByDealer(gold, silver);
        ArrangeDtc();
        ArrangeRequester();
        var repId = Guid.NewGuid();
        ArrangeCandidates(Candidate(repId, 10.0, 10.0, DateTime.UtcNow.AddHours(-1)));
        ArrangeBusyRepSequence(Array.Empty<Guid>(), new[] { repId });
        var service = CreateService();

        // Act
        await service.RunForPendingByDealerAsync(DealerId);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == gold.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == silver.Id),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenOlderAndNewerGoldRequestsPending_WhenRunForPendingByDealer_ThenOlderGoldIsOfferedFirst()
    {
        // Arrange
        var goldOld = BuildRequest(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-5));
        var goldNew = BuildRequest(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-1));
        ArrangeRequest(goldOld);
        ArrangeRequest(goldNew);
        ArrangePendingByDealer(goldOld, goldNew);
        ArrangeDtc();
        ArrangeRequester();
        var repId = Guid.NewGuid();
        ArrangeCandidates(Candidate(repId, 10.0, 10.0, DateTime.UtcNow.AddHours(-1)));
        ArrangeBusyRepSequence(Array.Empty<Guid>(), new[] { repId });
        var service = CreateService();

        // Act
        await service.RunForPendingByDealerAsync(DealerId);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == goldOld.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == goldNew.Id),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTwoGoldsAndTwoSilversPending_WhenRunForPendingByDealer_ThenBothGoldsAssignedBeforeSilvers()
    {
        // Arrange
        var gold1 = BuildRequest(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-4));
        var gold2 = BuildRequest(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-3));
        var silver1 = BuildRequest(ServiceTier.Silver, DateTime.UtcNow.AddMinutes(-2));
        var silver2 = BuildRequest(ServiceTier.Silver, DateTime.UtcNow.AddMinutes(-1));
        ArrangeRequest(gold1);
        ArrangeRequest(gold2);
        ArrangeRequest(silver1);
        ArrangeRequest(silver2);
        ArrangePendingByDealer(gold1, gold2, silver1, silver2);
        ArrangeDtc();
        ArrangeRequester();
        var rep1 = Guid.NewGuid();
        var rep2 = Guid.NewGuid();
        ArrangeCandidates(
            Candidate(rep1, 10.0, 10.0, DateTime.UtcNow.AddHours(-2)),
            Candidate(rep2, 10.0, 10.0, DateTime.UtcNow.AddHours(-1)));
        ArrangeBusyRepSequence(
            Array.Empty<Guid>(),
            new[] { rep1 },
            new[] { rep1, rep2 },
            new[] { rep1, rep2 });
        var service = CreateService();

        // Act
        await service.RunForPendingByDealerAsync(DealerId);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == gold1.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == gold2.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == silver1.Id || o.ServiceRequestId == silver2.Id),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTwoGoldRequestsWithDifferentNearestReps_WhenRunForPendingByDealer_ThenDistanceOrderingWithinTierPreserved()
    {
        // Arrange
        // AC-4: within a single tier, the nearest-rep (Haversine) selection is unaffected by the
        // tier-arbitration change — each Gold still routes to the rep closest to its own location.
        var gold1 = BuildRequest(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-2), lat: 10.0, lng: 10.0);
        var gold2 = BuildRequest(ServiceTier.Gold, DateTime.UtcNow.AddMinutes(-1), lat: 20.0, lng: 20.0);
        ArrangeRequest(gold1);
        ArrangeRequest(gold2);
        ArrangePendingByDealer(gold1, gold2);
        ArrangeDtc();
        ArrangeRequester();
        var repNearGold1 = Guid.NewGuid();
        var repNearGold2 = Guid.NewGuid();
        ArrangeCandidates(
            Candidate(repNearGold1, 10.1, 10.1, DateTime.UtcNow.AddHours(-1)),
            Candidate(repNearGold2, 20.1, 20.1, DateTime.UtcNow.AddHours(-1)));
        ArrangeBusyRepSequence(Array.Empty<Guid>(), new[] { repNearGold1 });
        var service = CreateService();

        // Act
        await service.RunForPendingByDealerAsync(DealerId);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == gold1.Id && o.RepId == repNearGold1),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == gold2.Id && o.RepId == repNearGold2),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
