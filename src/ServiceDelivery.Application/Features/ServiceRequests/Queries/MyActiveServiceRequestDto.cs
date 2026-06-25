namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public record MyActiveServiceRequestDto(
    Guid RequestId,
    string Tier,
    string DtcTitle,
    string Status,
    string RepState,
    double RequesterLatitude,
    double RequesterLongitude,
    DateTime CreatedAt
);
