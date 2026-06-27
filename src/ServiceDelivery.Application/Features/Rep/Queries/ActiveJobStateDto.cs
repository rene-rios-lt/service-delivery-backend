namespace ServiceDelivery.Application.Features.Rep.Queries;

public record ActiveJobStateDto(
    Guid RequestId,
    string RequesterName,
    string DtcTitle,
    double RequesterLat,
    double RequesterLng,
    double RepLat,
    double RepLng,
    int EtaMinutes,
    double DistanceMiles,
    string Tier,
    string RepState);
