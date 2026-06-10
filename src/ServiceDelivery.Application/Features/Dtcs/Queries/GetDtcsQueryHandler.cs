using MediatR;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Features.Dtcs.Queries;

public class GetDtcsQueryHandler : IRequestHandler<GetDtcsQuery, IReadOnlyList<DtcDto>>
{
    private readonly IDiagnosticTroubleCodeRepository _repository;

    public GetDtcsQueryHandler(IDiagnosticTroubleCodeRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<DtcDto>> Handle(GetDtcsQuery request, CancellationToken cancellationToken)
    {
        var dtcs = await _repository.GetAllByDealerIdAsync(request.DealerId, cancellationToken);

        return dtcs.Select(d => new DtcDto(
            d.Id,
            d.Code,
            d.HumanReadableTitle,
            d.RequiredEquipmentType.ToString()
        )).ToList();
    }
}
