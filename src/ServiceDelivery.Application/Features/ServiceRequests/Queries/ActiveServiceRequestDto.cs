namespace ServiceDelivery.Application.Features.ServiceRequests.Queries;

public record ActiveServiceRequestDto(
    Guid RequestId,
    string RequesterName,
    string Tier,
    string DtcTitle,
    string Status,
    Guid? AssignedRepId,
    string? AssignedRepName,
    DateTime CreatedAt
);
