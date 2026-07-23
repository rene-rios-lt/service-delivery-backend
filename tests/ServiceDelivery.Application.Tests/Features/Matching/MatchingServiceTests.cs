using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Interfaces.Payloads;
using ServiceDelivery.Application.Common.Services;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Domain.Projections;

namespace ServiceDelivery.Application.Tests.Features.Matching;

public class MatchingServiceTests
{
    private readonly Mock<IServiceRequestRepository> _requests = new();
    private readonly Mock<IDiagnosticTroubleCodeRepository> _dtcs = new();
    private readonly Mock<IRepStateRepository> _repStates = new();
    private readonly Mock<IJobOfferRepository> _jobOffers = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();

    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid OtherDealerId = Guid.NewGuid();
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

    private ServiceRequest BuildRequest(Guid? id = null, double lat = 10.0, double lng = 10.0)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            DealerId = DealerId,
            RequesterId = RequesterId,
            DtcId = DtcId,
            Latitude = lat,
            Longitude = lng,
            Status = ServiceRequestStatus.Pending,
            Tier = ServiceTier.Gold,
            CreatedAt = DateTime.UtcNow
        };

    private void ArrangeRequest(ServiceRequest request)
        => _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

    private void ArrangeDtc(EquipmentType required = EquipmentType.HydraulicTool, string title = "Hydraulic system fault")
        => _dtcs.Setup(d => d.GetByIdAsync(DtcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticTroubleCode
            {
                Id = DtcId,
                DealerId = DealerId,
                Code = "DTC-001",
                HumanReadableTitle = title,
                RequiredEquipmentType = required
            });

    private void ArrangeRequester(string name = "Gold User 1")
        => _users.Setup(u => u.FindByIdAsync(RequesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = RequesterId,
                Name = name,
                Email = "gold1@example.com",
                PasswordHash = "x",
                Role = UserRole.Requester,
                Tier = ServiceTier.Gold,
                DealerId = DealerId
            });

    private void ArrangeCandidates(Guid dealerId, params RepMatchCandidate[] candidates)
        => _repStates.Setup(r => r.GetAvailableByDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

    private void ArrangeSkipped(Guid requestId, params Guid[] skippedRepIds)
        => _jobOffers.Setup(j => j.GetSkippedRepIdsForRequestAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skippedRepIds);

    private void ArrangeLivePendingOffer(Guid requestId, JobOffer? offer = null)
        => _jobOffers.Setup(j => j.GetLivePendingOfferForRequestAsync(
                requestId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

    private static RepMatchCandidate Candidate(
        Guid repId,
        double lat,
        double lng,
        DateTime availableSince,
        params EquipmentType[] equipment)
        => new(repId, lat, lng, equipment.Length == 0 ? new[] { EquipmentType.HydraulicTool } : equipment, availableSince);

    [Fact]
    public async Task GivenRepsAcrossTwoDealers_WhenMatching_ThenOnlySameDealerRepsConsidered()
    {
        // Arrange
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var sameDealerRep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(sameDealerRep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _repStates.Verify(r => r.GetAvailableByDealerAsync(DealerId, It.IsAny<CancellationToken>()), Times.Once);
        _repStates.Verify(r => r.GetAvailableByDealerAsync(OtherDealerId, It.IsAny<CancellationToken>()), Times.Never);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == sameDealerRep),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenRepsWithAndWithoutRequiredEquipment_WhenMatching_ThenOnlyEquippedRepsConsidered()
    {
        // Arrange
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc(EquipmentType.BrakingSystemKit);
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var equippedRep = Guid.NewGuid();
        var unequippedRep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(unequippedRep, 10.0, 10.0, DateTime.UtcNow.AddHours(-2), EquipmentType.HydraulicTool),
            Candidate(equippedRep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.BrakingSystemKit));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == equippedRep),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == unequippedRep),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAnAvailableRep_WhenMatching_ThenCandidatesSourcedFromAvailableFilteredQuery()
    {
        // Arrange
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var availableRep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(availableRep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _repStates.Verify(r => r.GetAvailableByDealerAsync(request.DealerId, It.IsAny<CancellationToken>()), Times.Once);
        _repStates.Verify(r => r.GetByRepIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == availableRep),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARepDeclinedThisRequest_WhenMatching_ThenThatRepIsExcluded()
    {
        // Arrange
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        var declinedRep = Guid.NewGuid();
        var freshRep = Guid.NewGuid();
        ArrangeSkipped(request.Id, declinedRep);
        ArrangeCandidates(DealerId,
            Candidate(declinedRep, 10.0, 10.0, DateTime.UtcNow.AddHours(-2), EquipmentType.HydraulicTool),
            Candidate(freshRep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == freshRep),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == declinedRep),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAllQualifiedRepsHaveOnlyExpiredOffers_WhenRunAsync_ThenWinnerIsSelectedAndOfferCreated()
    {
        // Arrange
        // BUG-054: after the fix the repository no longer counts expired offers in the skip list, so a
        // request whose only qualified rep merely let an offer expire is represented by an EMPTY skip list.
        // The matcher must find a winner and create an offer — never fall silently into the pending path.
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        // BUG-058: no LIVE Pending offer exists — an expired offer must not block re-offering (BUG-054).
        ArrangeLivePendingOffer(request.Id);
        var previouslyExpiredRep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(previouslyExpiredRep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == previouslyExpiredRep && o.Status == JobOfferStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);
        _dispatchHub.Verify(h => h.SendServiceRequestPendingAsync(
            It.IsAny<string>(), It.IsAny<ServiceRequestPendingPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenRepHasDeclinedAndAnotherRepHasExpiredOffer_WhenRunAsync_ThenOnlyDeclinedRepIsExcluded()
    {
        // Arrange
        // BUG-054: the declined rep stays in the skip list (permanent opt-out); the expired rep does not,
        // so the expired rep is the eligible winner and the declined rep is excluded.
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        var declinedRep = Guid.NewGuid();
        var previouslyExpiredRep = Guid.NewGuid();
        ArrangeSkipped(request.Id, declinedRep);
        // BUG-058: no LIVE Pending offer exists — declined/expired offers must not block re-offering (BUG-054).
        ArrangeLivePendingOffer(request.Id);
        ArrangeCandidates(DealerId,
            Candidate(declinedRep, 10.0, 10.0, DateTime.UtcNow.AddHours(-2), EquipmentType.HydraulicTool),
            Candidate(previouslyExpiredRep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == previouslyExpiredRep),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == declinedRep),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenMultipleCandidatesAtDifferentDistances_WhenMatching_ThenNearestRepReceivesOffer()
    {
        // Arrange
        var request = BuildRequest(lat: 10.0, lng: 10.0);
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var farRep = Guid.NewGuid();
        var nearRep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(farRep, 12.0, 12.0, DateTime.UtcNow, EquipmentType.HydraulicTool),
            Candidate(nearRep, 10.1, 10.1, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == nearRep),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == farRep),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTwoEquidistantReps_WhenMatching_ThenRepAvailableLongestReceivesOffer()
    {
        // Arrange
        var request = BuildRequest(lat: 10.0, lng: 10.0);
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var recentlyAvailableRep = Guid.NewGuid();
        var longestAvailableRep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(recentlyAvailableRep, 10.1, 10.1, new DateTime(2026, 6, 11, 9, 0, 0, DateTimeKind.Utc), EquipmentType.HydraulicTool),
            Candidate(longestAvailableRep, 10.1, 10.1, new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc), EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == longestAvailableRep),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == recentlyAvailableRep),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAMatchingRep_WhenMatching_ThenJobOfferPersistedWithStatusPending()
    {
        // Arrange
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o =>
                o.ServiceRequestId == request.Id
                && o.RepId == rep
                && o.Status == JobOfferStatus.Pending
                && o.ExpiresAt > o.OfferedAt),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAMatchingRep_WhenMatching_ThenJobOfferReceivedSentToRep()
    {
        // Arrange
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc(EquipmentType.HydraulicTool, "Hydraulic system fault");
        ArrangeRequester("Gold User 1");
        ArrangeSkipped(request.Id);
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _repHub.Verify(h => h.SendJobOfferReceivedAsync(
            $"rep:{rep}",
            It.Is<JobOfferReceivedPayload>(p =>
                p.RequestId == request.Id
                && p.RequesterName == "Gold User 1"
                && p.RequesterTier == "Gold"
                && p.DtcTitle == "Hydraulic system fault"
                && p.Latitude == request.Latitude
                && p.Longitude == request.Longitude),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoCandidateReps_WhenMatching_ThenNoOfferCreatedAndDispatchersNotified()
    {
        // Arrange
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc(EquipmentType.HydraulicTool, "Hydraulic system fault");
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        ArrangeCandidates(DealerId);
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatchHub.Verify(h => h.SendServiceRequestPendingAsync(
            $"dealer:{request.DealerId}",
            It.Is<ServiceRequestPendingPayload>(p =>
                p.RequestId == request.Id
                && p.RequesterTier == "Gold"
                && p.DtcTitle == "Hydraulic system fault"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoCandidateReps_WhenMatching_ThenServiceRequestPendingSentToDispatchers()
    {
        // Arrange
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc(EquipmentType.HydraulicTool, "Hydraulic system fault");
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        ArrangeCandidates(DealerId);
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _dispatchHub.Verify(h => h.SendServiceRequestPendingAsync(
            $"dealer:{request.DealerId}",
            It.Is<ServiceRequestPendingPayload>(p =>
                p.RequestId == request.Id
                && p.RequesterTier == "Gold"
                && p.DtcTitle == "Hydraulic system fault"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenOfferExpirySecondsConfigured_WhenOfferCreated_ThenExpiresAtReflectsConfiguredValue()
    {
        // Arrange
        const int ConfiguredExpirySeconds = 5;
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        JobOffer? captured = null;
        _jobOffers.Setup(j => j.AddAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()))
            .Callback<JobOffer, CancellationToken>((o, _) => captured = o)
            .Returns(Task.CompletedTask);
        var service = CreateService(offerExpirySeconds: ConfiguredExpirySeconds);

        // Act
        var before = DateTime.UtcNow;
        await service.RunAsync(request.Id);
        var after = DateTime.UtcNow;

        // Assert
        captured.Should().NotBeNull();
        captured!.ExpiresAt.Should().BeOnOrAfter(before.AddSeconds(ConfiguredExpirySeconds));
        captured.ExpiresAt.Should().BeOnOrBefore(after.AddSeconds(ConfiguredExpirySeconds));
    }

    [Fact]
    public async Task GivenDefaultOfferExpirySeconds_WhenOfferCreated_ThenExpiresAtIsSixtySecondsAhead()
    {
        // Arrange
        const int DefaultExpirySeconds = 60;
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        JobOffer? captured = null;
        _jobOffers.Setup(j => j.AddAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()))
            .Callback<JobOffer, CancellationToken>((o, _) => captured = o)
            .Returns(Task.CompletedTask);
        var service = CreateService();

        // Act
        var before = DateTime.UtcNow;
        await service.RunAsync(request.Id);
        var after = DateTime.UtcNow;

        // Assert
        captured.Should().NotBeNull();
        captured!.ExpiresAt.Should().BeOnOrAfter(before.AddSeconds(DefaultExpirySeconds));
        captured.ExpiresAt.Should().BeOnOrBefore(after.AddSeconds(DefaultExpirySeconds));
    }

    [Fact]
    public async Task GivenMultiplePendingRequestsForDealer_WhenRunForPendingByDealer_ThenEachIsRematched()
    {
        // Arrange
        var request1 = BuildRequest();
        var request2 = BuildRequest();
        ArrangeRequest(request1);
        ArrangeRequest(request2);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request1.Id);
        ArrangeSkipped(request2.Id);
        _requests.Setup(r => r.GetPendingByDealerAsync(DealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { request1, request2 });
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunForPendingByDealerAsync(DealerId);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == request1.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == request2.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenARequestWithALivePendingOffer_WhenRunAsyncCalled_ThenNoSecondOfferCreated()
    {
        // Arrange
        // BUG-058: even with a valid winner present, a live (unexpired) Pending offer already exists
        // for this request, so the matcher must short-circuit before constructing a second offer.
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        ArrangeLivePendingOffer(request.Id, new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            RepId = Guid.NewGuid(),
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(1),
            Status = JobOfferStatus.Pending
        });
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Never);
        _repHub.Verify(h => h.SendJobOfferReceivedAsync(
            It.IsAny<string>(), It.IsAny<JobOfferReceivedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTwoSequentialRunAsyncCalls_WhenSecondCallFindsLiveOffer_ThenOnlyOneOfferCreated()
    {
        // Arrange
        // BUG-058: the deterministic stand-in for two near-simultaneous matching passes (the true
        // sub-millisecond race is a documented POC boundary needing a DB unique index). First pass finds
        // no live offer and creates one; second pass finds the now-live offer and returns before creating.
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var createdOffer = new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            RepId = rep,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(1),
            Status = JobOfferStatus.Pending
        };
        _jobOffers.SetupSequence(j => j.GetLivePendingOfferForRequestAsync(
                request.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobOffer?)null)
            .ReturnsAsync(createdOffer);
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(It.IsAny<JobOffer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenRequestWithNoLivePendingOffer_WhenRunAsyncCalled_ThenExactlyOneOfferCreated()
    {
        // Arrange
        // BUG-058: the no-live-offer path proceeds normally and creates exactly one offer.
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        ArrangeLivePendingOffer(request.Id);
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.ServiceRequestId == request.Id && o.RepId == rep),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenRequestWithOnlyExpiredStatusOffer_WhenRunAsyncCalled_ThenNewOfferStillCreated()
    {
        // Arrange
        // BUG-058/BUG-054: an Expired-status offer is never "live" — GetLivePendingOfferForRequestAsync
        // returns null (the repository Status filter excludes it), so a new offer is still created.
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        ArrangeLivePendingOffer(request.Id);
        var rep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(rep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == rep && o.Status == JobOfferStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenRequestWithOnlyDeclinedOffer_WhenRunAsyncCalled_ThenNewOfferStillCreated()
    {
        // Arrange
        // BUG-058/BUG-054: a Declined offer is never "live" — GetLivePendingOfferForRequestAsync returns
        // null, so a fresh (non-declined) rep still receives a new offer; dedup must not starve the request.
        var request = BuildRequest();
        ArrangeRequest(request);
        ArrangeDtc();
        ArrangeRequester();
        ArrangeSkipped(request.Id);
        ArrangeLivePendingOffer(request.Id);
        var freshRep = Guid.NewGuid();
        ArrangeCandidates(DealerId,
            Candidate(freshRep, 10.0, 10.0, DateTime.UtcNow, EquipmentType.HydraulicTool));
        var service = CreateService();

        // Act
        await service.RunAsync(request.Id);

        // Assert
        _jobOffers.Verify(j => j.AddAsync(
            It.Is<JobOffer>(o => o.RepId == freshRep && o.Status == JobOfferStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
