using FluentAssertions;
using Moq;
using ServiceDelivery.Application.Features.Dtcs.Queries;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Domain.Interfaces;

namespace ServiceDelivery.Application.Tests.Features.Dtcs;

public class GetDtcsQueryHandlerTests
{
    private readonly Mock<IDiagnosticTroubleCodeRepository> _repositoryMock;
    private readonly GetDtcsQueryHandler _handler;

    public GetDtcsQueryHandlerTests()
    {
        _repositoryMock = new Mock<IDiagnosticTroubleCodeRepository>();
        _handler = new GetDtcsQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task GivenDtcsExistForDealer_WhenGetDtcsHandled_ThenOnlyDealerDtcsAreReturned()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var dtcs = new List<DiagnosticTroubleCode>
        {
            new() { Id = Guid.NewGuid(), DealerId = dealerId, Code = "DTC-001", HumanReadableTitle = "Hydraulic fault", RequiredEquipmentType = EquipmentType.HydraulicTool },
            new() { Id = Guid.NewGuid(), DealerId = dealerId, Code = "DTC-002", HumanReadableTitle = "Electrical fault", RequiredEquipmentType = EquipmentType.ElectricalDiagnosticKit },
        };

        _repositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtcs);

        var query = new GetDtcsQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.All(d => d.Id != Guid.Empty).Should().BeTrue();
    }

    [Fact]
    public async Task GivenDtcsFromTwoDealers_WhenGetDtcsHandled_ThenOtherDealerDtcsAreExcluded()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var otherDealerId = Guid.NewGuid();

        var callerDtcs = new List<DiagnosticTroubleCode>
        {
            new() { Id = Guid.NewGuid(), DealerId = dealerId, Code = "DTC-001", HumanReadableTitle = "Hydraulic fault", RequiredEquipmentType = EquipmentType.HydraulicTool },
        };

        _repositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerDtcs);

        _repositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(otherDealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DiagnosticTroubleCode>
            {
                new() { Id = Guid.NewGuid(), DealerId = otherDealerId, Code = "DTC-X01", HumanReadableTitle = "Other fault", RequiredEquipmentType = EquipmentType.HydraulicTool },
            });

        var query = new GetDtcsQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.All(d => d.Id != Guid.Empty).Should().BeTrue();
    }

    [Fact]
    public async Task GivenADtc_WhenGetDtcsHandled_ThenDtoContainsAllRequiredFields()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var dtcId = Guid.NewGuid();
        var dtcs = new List<DiagnosticTroubleCode>
        {
            new() { Id = dtcId, DealerId = dealerId, Code = "DTC-001", HumanReadableTitle = "Hydraulic fault", RequiredEquipmentType = EquipmentType.HydraulicTool },
        };

        _repositoryMock
            .Setup(r => r.GetAllByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtcs);

        var query = new GetDtcsQuery(dealerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Id.Should().Be(dtcId);
        dto.Code.Should().Be("DTC-001");
        dto.Title.Should().Be("Hydraulic fault");
        dto.RequiredEquipment.Should().Be("HydraulicTool");
    }
}
