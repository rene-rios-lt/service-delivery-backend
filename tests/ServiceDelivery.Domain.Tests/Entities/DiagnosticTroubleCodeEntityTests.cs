using FluentAssertions;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;

namespace ServiceDelivery.Domain.Tests.Entities;

public class DiagnosticTroubleCodeEntityTests
{
    [Fact]
    public void GivenANewDtc_WhenConstructed_ThenCodeIsEmpty()
    {
        // Arrange
        var dtc = new DiagnosticTroubleCode();

        // Act
        var code = dtc.Code;

        // Assert
        code.Should().BeEmpty();
    }

    [Fact]
    public void GivenANewDtc_WhenConstructed_ThenHumanReadableTitleIsEmpty()
    {
        // Arrange
        var dtc = new DiagnosticTroubleCode();

        // Act
        var title = dtc.HumanReadableTitle;

        // Assert
        title.Should().BeEmpty();
    }

    [Fact]
    public void GivenADtc_WhenRequiredEquipmentTypeSet_ThenEquipmentTypeIsStored()
    {
        // Arrange
        var dtc = new DiagnosticTroubleCode();

        // Act
        dtc.RequiredEquipmentType = EquipmentType.HydraulicTool;

        // Assert
        dtc.RequiredEquipmentType.Should().Be(EquipmentType.HydraulicTool);
    }

    [Fact]
    public void GivenADtc_WhenCodeAndTitleSet_ThenValuesAreStored()
    {
        // Arrange
        var dtc = new DiagnosticTroubleCode();

        // Act
        dtc.Code = "DTC-001";
        dtc.HumanReadableTitle = "Hydraulic system fault";

        // Assert
        dtc.Code.Should().Be("DTC-001");
        dtc.HumanReadableTitle.Should().Be("Hydraulic system fault");
    }
}
