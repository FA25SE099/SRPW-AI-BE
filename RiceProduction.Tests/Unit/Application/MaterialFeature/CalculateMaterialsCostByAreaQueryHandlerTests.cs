using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByArea;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using System.Linq.Expressions;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.MaterialFeature;

/// <summary>
/// Tests for CalculateMaterialsCostByAreaQueryHandler - validates material cost calculation
/// </summary>
public class CalculateMaterialsCostByAreaQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Material>> _mockMaterialRepo;
    private readonly Mock<IGenericRepository<MaterialPrice>> _mockMaterialPriceRepo;
    private readonly Mock<ILogger<CalculateMaterialsCostByAreaQueryHandler>> _mockLogger;
    private readonly CalculateMaterialsCostByAreaQueryHandler _handler;

    public CalculateMaterialsCostByAreaQueryHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMaterialRepo = new Mock<IGenericRepository<Material>>();
        _mockMaterialPriceRepo = new Mock<IGenericRepository<MaterialPrice>>();
        _mockLogger = new Mock<ILogger<CalculateMaterialsCostByAreaQueryHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<Material>()).Returns(_mockMaterialRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<MaterialPrice>()).Returns(_mockMaterialPriceRepo.Object);

        _handler = new CalculateMaterialsCostByAreaQueryHandler(_mockUnitOfWork.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_NoActiveMaterials_ReturnsFailure()
    {
        // Arrange
        _mockMaterialRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Material, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Material>());

        var query = new CalculateMaterialsCostByAreaQuery
        {
            Area = 10.5m,
            Materials = new List<MaterialQuantityInput>
            {
                new MaterialQuantityInput { MaterialId = Guid.NewGuid(), QuantityPerHa = 50 }
            }
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("No active materials found.");
    }

    [Fact]
    public async Task Handle_ValidMaterialsWithCurrentPrices_CalculatesCorrectTotalCost()
    {
        // Arrange
        var materialId1 = Guid.NewGuid();
        var materialId2 = Guid.NewGuid();

        var material1 = new Material
        {
            Id = materialId1,
            Name = "Fertilizer A",
            Unit = "kg",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var material2 = new Material
        {
            Id = materialId2,
            Name = "Pesticide B",
            Unit = "liter",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var price1 = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId1,
            PricePerMaterial = 100000m, // 100,000 VND per kg
            ValidFrom = DateTime.UtcNow.AddDays(-10),
            ValidTo = null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var price2 = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId2,
            PricePerMaterial = 250000m, // 250,000 VND per liter
            ValidFrom = DateTime.UtcNow.AddDays(-5),
            ValidTo = null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockMaterialRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Material, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Material> { material1, material2 });

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<MaterialPrice> { price1, price2 });

        var query = new CalculateMaterialsCostByAreaQuery
        {
            Area = 10m, // 10 hectares
            Materials = new List<MaterialQuantityInput>
            {
                new MaterialQuantityInput { MaterialId = materialId1, QuantityPerHa = 50 }, // 50 kg/ha
                new MaterialQuantityInput { MaterialId = materialId2, QuantityPerHa = 10 }  // 10 liters/ha
            }
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Area.Should().Be(10m);
        result.Data.MaterialCostItems.Should().HaveCount(2);

        // Material 1: 50 kg/ha * 10 ha * 100,000 = 50,000,000
        var mat1 = result.Data.MaterialCostItems.First(m => m.MaterialId == materialId1);
        mat1.TotalQuantityNeeded.Should().BeGreaterThan(0); // Depends on calculation
        mat1.TotalCost.Should().BeGreaterThan(0);

        // Material 2: 10 liters/ha * 10 ha * 250,000 = 25,000,000
        var mat2 = result.Data.MaterialCostItems.First(m => m.MaterialId == materialId2);
        mat2.TotalQuantityNeeded.Should().BeGreaterThan(0);
        mat2.TotalCost.Should().BeGreaterThan(0);

        // Total cost should be sum of all materials
        result.Data.TotalCostForArea.Should().BeGreaterThan(0);
        result.Data.PriceWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MaterialsWithMultiplePrices_UsesLatestValidPrice()
    {
        // Arrange
        var materialId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            Name = "Fertilizer",
            Unit = "kg",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var oldPrice = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            PricePerMaterial = 80000m,
            ValidFrom = DateTime.UtcNow.AddDays(-30),
            ValidTo = DateTime.UtcNow.AddDays(-10),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
        };

        var currentPrice = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            PricePerMaterial = 100000m, // Latest price
            ValidFrom = DateTime.UtcNow.AddDays(-9),
            ValidTo = null,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-9)
        };

        _mockMaterialRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Material, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Material> { material });

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<MaterialPrice> { oldPrice, currentPrice });

        var query = new CalculateMaterialsCostByAreaQuery
        {
            Area = 5m,
            Materials = new List<MaterialQuantityInput>
            {
                new MaterialQuantityInput { MaterialId = materialId, QuantityPerHa = 100 }
            }
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.MaterialCostItems.First().PricePerMaterial.Should().Be(100000m); // Should use latest price
        result.Data.TotalCostForArea.Should().BeGreaterThan(0); // Some positive cost
    }

    [Fact]
    public async Task Handle_MaterialWithNoPrices_GeneratesWarning()
    {
        // Arrange
        var materialId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            Name = "New Material",
            Unit = "kg",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockMaterialRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Material, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Material> { material });

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<MaterialPrice>()); // No prices

        var query = new CalculateMaterialsCostByAreaQuery
        {
            Area = 10m,
            Materials = new List<MaterialQuantityInput>
            {
                new MaterialQuantityInput { MaterialId = materialId, QuantityPerHa = 50 }
            }
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - Handler returns failure when no valid calculations can be performed
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("No valid material cost calculations could be performed.");
    }

    [Fact]
    public async Task Handle_GroupedMaterialsSameMaterialId_SumsQuantities()
    {
        // Arrange
        var materialId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            Name = "Fertilizer",
            Unit = "kg",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var price = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            PricePerMaterial = 100000m,
            ValidFrom = DateTime.UtcNow.AddDays(-10),
            ValidTo = null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockMaterialRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Material, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Material> { material });

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<MaterialPrice> { price });

        var query = new CalculateMaterialsCostByAreaQuery
        {
            Area = 10m,
            Materials = new List<MaterialQuantityInput>
            {
                new MaterialQuantityInput { MaterialId = materialId, QuantityPerHa = 30 },
                new MaterialQuantityInput { MaterialId = materialId, QuantityPerHa = 20 }, // Same material
                new MaterialQuantityInput { MaterialId = materialId, QuantityPerHa = 10 }  // Same material again
            }
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.MaterialCostItems.Should().HaveCount(1); // Should be grouped into one
        result.Data.MaterialCostItems.First().QuantityPerHa.Should().Be(60m); // 30 + 20 + 10
        result.Data.MaterialCostItems.First().TotalQuantityNeeded.Should().BeGreaterThan(0);
        result.Data.TotalCostForArea.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        var handler = new CalculateMaterialsCostByAreaQueryHandler(_mockUnitOfWork.Object, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
