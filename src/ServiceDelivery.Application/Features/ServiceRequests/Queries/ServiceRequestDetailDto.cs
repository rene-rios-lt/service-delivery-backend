namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public record ServiceRequestDetailDto(
    Guid RequestId,
    string RequesterName,
    string Tier,
    string DtcTitle,
    LocationDto RequesterLocation,
    string Status,
    AssignedRepDto? AssignedRep,
    DateTime CreatedAt,
    IReadOnlyList<JobOfferHistoryDto> OfferHistory);

public record LocationDto(double Lat, double Lng);

public record AssignedRepDto(Guid RepId, string Name);

public record JobOfferHistoryDto(
    Guid OfferId,
    Guid RepId,
    string RepName,
    string Status,
    DateTime OfferedAt,
    DateTime ExpiresAt);
