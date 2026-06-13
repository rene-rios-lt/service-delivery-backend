using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Common;
using ServiceDelivery.Application.Features.JobOffers.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.JobOffers;

public class GetPendingJobOfferQueryHandlerTests
{
    private readonly Mock<IJobOfferRepository> _jobOfferRepositoryMock;
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepositoryMock;
    private readonly Mock<IDiagnosticTroubleCodeRepository> _dtcRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IVehicleRepository> _vehicleRepositoryMock;
    private readonly GetPendingJobOfferQueryHandler _handler;

    public GetPendingJobOfferQueryHandlerTests()
    {
        _jobOfferRepositoryMock = new Mock<IJobOfferRepository>();
        _serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        _dtcRepositoryMock = new Mock<IDiagnosticTroubleCodeRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _vehicleRepositoryMock = new Mock<IVehicleRepository>();
        _handler = new GetPendingJobOfferQueryHandler(
            _jobOfferRepositoryMock.Object,
            _serviceRequestRepositoryMock.Object,
            _dtcRepositoryMock.Object,
            _userRepositoryMock.Object,
            _vehicleRepositoryMock.Object);
    }

    private void SetupOffer(
        Guid repId,
        JobOffer offer,
        ServiceRequest request,
        DiagnosticTroubleCode dtc,
        User requester)
    {
        _jobOfferRepositoryMock
            .Setup(r => r.GetPendingByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _serviceRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _dtcRepositoryMock
            .Setup(r => r.GetByIdAsync(dtc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtc);
        _userRepositoryMock
            .Setup(r => r.FindByIdAsync(requester.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);
    }

    [Fact]
    public async Task GivenARepWithOnePendingOffer_WhenGetPendingCalled_ThenThatOfferIsReturned()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var offer = new JobOffer
        {
            Id = offerId,
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = JobOfferStatus.Pending
        };
        var request = new ServiceRequest
        {
            Id = requestId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = ServiceTier.Bronze,
            Latitude = 41.5,
            Longitude = -93.6
        };
        var dtc = new DiagnosticTroubleCode { Id = dtcId, HumanReadableTitle = "Hydraulic fault" };
        var requester = new User { Id = requesterId, Name = "Gold User 1" };
        SetupOffer(repId, offer, request, dtc, requester);

        var query = new GetPendingJobOfferQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OfferId.Should().Be(offerId);
    }

    [Fact]
    public async Task GivenAPendingOffer_WhenGetPendingCalled_ThenDtoContainsAllRequiredFields()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var expiresAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);

        var offer = new JobOffer
        {
            Id = offerId,
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            Status = JobOfferStatus.Pending
        };
        var request = new ServiceRequest
        {
            Id = requestId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = ServiceTier.Gold,
            Latitude = 41.878,
            Longitude = -93.097
        };
        var dtc = new DiagnosticTroubleCode { Id = dtcId, HumanReadableTitle = "Hydraulic system fault" };
        var requester = new User { Id = requesterId, Name = "Gold User 1" };
        SetupOffer(repId, offer, request, dtc, requester);

        var query = new GetPendingJobOfferQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OfferId.Should().Be(offerId);
        result.RequesterName.Should().Be("Gold User 1");
        result.Tier.Should().Be("Gold");
        result.DtcTitle.Should().Be("Hydraulic system fault");
        result.RequesterLocation.Lat.Should().Be(41.878);
        result.RequesterLocation.Lng.Should().Be(-93.097);
        result.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GivenAPendingOfferAndRepVehiclePosition_WhenGetPendingCalled_ThenDistanceAndEtaAreComputed()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var offer = new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = JobOfferStatus.Pending
        };
        var request = new ServiceRequest
        {
            Id = requestId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = ServiceTier.Silver,
            Latitude = 41.6,
            Longitude = -93.6
        };
        var dtc = new DiagnosticTroubleCode { Id = dtcId, HumanReadableTitle = "Electrical fault" };
        var requester = new User { Id = requesterId, Name = "Silver User 1" };
        SetupOffer(repId, offer, request, dtc, requester);

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            ClaimedByRepId = repId,
            LastLatitude = 41.5,
            LastLongitude = -93.6
        };
        _vehicleRepositoryMock
            .Setup(r => r.GetByClaimedRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        var expectedDistance = HaversineCalculator.DistanceMiles(41.5, -93.6, 41.6, -93.6);
        var expectedEta = HaversineCalculator.EtaMinutes(expectedDistance);

        var query = new GetPendingJobOfferQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DistanceMiles.Should().BeApproximately(expectedDistance, 0.0001);
        result.EtaMinutes.Should().BeApproximately(expectedEta, 0.0001);
    }

    [Fact]
    public async Task GivenAPendingOfferAndNoVehiclePosition_WhenGetPendingCalled_ThenDistanceAndEtaAreNull()
    {
        // Arrange
        var repId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var offer = new JobOffer
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            RepId = repId,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = JobOfferStatus.Pending
        };
        var request = new ServiceRequest
        {
            Id = requestId,
            RequesterId = requesterId,
            DtcId = dtcId,
            Tier = ServiceTier.Bronze,
            Latitude = 41.6,
            Longitude = -93.6
        };
        var dtc = new DiagnosticTroubleCode { Id = dtcId, HumanReadableTitle = "Braking fault" };
        var requester = new User { Id = requesterId, Name = "Bronze User 1" };
        SetupOffer(repId, offer, request, dtc, requester);

        _vehicleRepositoryMock
            .Setup(r => r.GetByClaimedRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vehicle?)null);

        var query = new GetPendingJobOfferQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DistanceMiles.Should().BeNull();
        result.EtaMinutes.Should().BeNull();
    }

    [Fact]
    public async Task GivenARepWithNoPendingOffer_WhenGetPendingCalled_ThenResultIsNull()
    {
        // Arrange
        var repId = Guid.NewGuid();
        _jobOfferRepositoryMock
            .Setup(r => r.GetPendingByRepIdAsync(repId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobOffer?)null);

        var query = new GetPendingJobOfferQuery(repId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
