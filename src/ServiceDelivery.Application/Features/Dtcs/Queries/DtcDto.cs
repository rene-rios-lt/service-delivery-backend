namespace ServiceDelivery.Application.Features.Dtcs.Queries;

public record DtcDto(
    Guid Id,
    string Code,
    string Title,
    string RequiredEquipment);
