using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDelivery.Domain.Entities;
using ServiceDelivery.Domain.Enums;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Repositories;

namespace ServiceDelivery.Infrastructure.Tests.Repositories;

public class DiagnosticTroubleCodeRepositoryTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DtcTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GivenASeededDiagnosticTroubleCode_WhenGetByIdAsyncCalled_ThenReturnsDtcWithMatchingId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var dtcId = Guid.NewGuid();
        context.DiagnosticTroubleCodes.Add(new DiagnosticTroubleCode
        {
            Id = dtcId,
            DealerId = Guid.NewGuid(),
            Code = "DTC-TEST",
            HumanReadableTitle = "Test fault",
            RequiredEquipmentType = EquipmentType.HydraulicTool
        });
        await context.SaveChangesAsync();

        var repository = new DiagnosticTroubleCodeRepository(context);

        // Act
        var result = await repository.GetByIdAsync(dtcId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(dtcId);
        result.HumanReadableTitle.Should().Be("Test fault");
    }

    [Fact]
    public async Task GivenNoDiagnosticTroubleCodeWithId_WhenGetByIdAsyncCalled_ThenReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new DiagnosticTroubleCodeRepository(context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }
}
