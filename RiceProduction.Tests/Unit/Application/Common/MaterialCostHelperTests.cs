using FluentAssertions;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.Common;

public class MaterialCostHelperTests
{
    #region CalculatePackagesNeeded

    public static TheoryData<decimal, decimal, decimal> PartitionPackagesData => new()
    {
        { 25m, 10m, 2.5m },
        { 75m, 50m, 1.5m },
        { 1m, 4m, 0.25m },
        { 20m, 10m, 2m }
    };

    [Theory]
    [MemberData(nameof(PartitionPackagesData))]
    public void CalculatePackagesNeeded_WhenPartitionIsTrue_ReturnsExactFractionalPackages(
        decimal totalQuantityNeeded,
        decimal amountPerMaterial,
        decimal expectedPackages)
    {
        // Act
        var result = MaterialCostHelper.CalculatePackagesNeeded(
            totalQuantityNeeded,
            amountPerMaterial,
            isPartition: true);

        // Assert
        result.Should().Be(expectedPackages);
    }

    public static TheoryData<decimal, decimal, decimal> NonPartitionPackagesData => new()
    {
        { 25m, 10m, 3m },
        { 20.1m, 10m, 3m },
        { 20m, 10m, 2m },
        { 1m, 50m, 1m },
        { 0m, 10m, 0m }
    };

    [Theory]
    [MemberData(nameof(NonPartitionPackagesData))]
    public void CalculatePackagesNeeded_WhenPartitionIsFalse_ReturnsCeilingPackages(
        decimal totalQuantityNeeded,
        decimal amountPerMaterial,
        decimal expectedPackages)
    {
        // Act
        var result = MaterialCostHelper.CalculatePackagesNeeded(
            totalQuantityNeeded,
            amountPerMaterial,
            isPartition: false);

        // Assert
        result.Should().Be(expectedPackages);
    }

    #endregion

    #region CalculateMaterialCost

    [Fact]
    public void CalculateMaterialCost_ValidMaterialAndPrice_NonPartition_ReturnsExpectedCostItem()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var material = new Material
        {
            Id = materialId,
            Name = "NPK Fertilizer",
            Unit = "kg",
            AmmountPerMaterial = 50m,
            IsPartition = false
        };
        var price = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            PricePerMaterial = 100_000m,
            ValidFrom = validFrom
        };
        var materialsDict = new Dictionary<Guid, Material> { [materialId] = material };
        var currentPrices = new Dictionary<Guid, MaterialPrice> { [materialId] = price };
        var warnings = new List<string>();

        // Act
        var result = MaterialCostHelper.CalculateMaterialCost(
            materialId,
            quantityPerHa: 30m,
            area: 2.5m,
            materialsDict,
            currentPrices,
            warnings);

        // Assert
        result.Should().NotBeNull();
        warnings.Should().BeEmpty();
        result!.MaterialId.Should().Be(materialId);
        result.MaterialName.Should().Be("NPK Fertilizer");
        result.Unit.Should().Be("kg");
        result.QuantityPerHa.Should().Be(30m);
        result.TotalQuantityNeeded.Should().Be(75m);
        result.AmountPerMaterial.Should().Be(50m);
        result.PackagesNeeded.Should().Be(2m);
        result.ActualQuantity.Should().Be(100m);
        result.PricePerMaterial.Should().Be(100_000m);
        result.TotalCost.Should().Be(200_000m);
        result.CostPerHa.Should().Be(80_000m);
        result.PriceValidFrom.Should().Be(validFrom);
    }

    [Fact]
    public void CalculateMaterialCost_ValidMaterialAndPrice_Partition_AllowsFractionalPackages()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Liquid Herbicide",
            Unit = "ml",
            AmmountPerMaterial = 50m,
            IsPartition = true
        };
        var price = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            PricePerMaterial = 100_000m,
            ValidFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var materialsDict = new Dictionary<Guid, Material> { [materialId] = material };
        var currentPrices = new Dictionary<Guid, MaterialPrice> { [materialId] = price };
        var warnings = new List<string>();

        // Act
        var result = MaterialCostHelper.CalculateMaterialCost(
            materialId,
            quantityPerHa: 30m,
            area: 2.5m,
            materialsDict,
            currentPrices,
            warnings);

        // Assert
        result.Should().NotBeNull();
        warnings.Should().BeEmpty();
        result!.PackagesNeeded.Should().Be(1.5m);
        result.ActualQuantity.Should().Be(75m);
        result.TotalCost.Should().Be(150_000m);
        result.CostPerHa.Should().Be(60_000m);
    }

    [Fact]
    public void CalculateMaterialCost_MissingMaterial_AddsWarningAndReturnsNull()
    {
        // Arrange
        var missingMaterialId = Guid.NewGuid();
        var materialsDict = new Dictionary<Guid, Material>();
        var currentPrices = new Dictionary<Guid, MaterialPrice>();
        var warnings = new List<string>();

        // Act
        var result = MaterialCostHelper.CalculateMaterialCost(
            missingMaterialId,
            quantityPerHa: 10m,
            area: 2m,
            materialsDict,
            currentPrices,
            warnings);

        // Assert
        result.Should().BeNull();
        warnings.Should().ContainSingle()
            .Which.Should().Contain(missingMaterialId.ToString())
            .And.Contain("not found or is inactive");
    }

    [Fact]
    public void CalculateMaterialCost_MissingPrice_AddsWarningAndReturnsNull()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Urea",
            Unit = "kg",
            AmmountPerMaterial = 50m
        };
        var materialsDict = new Dictionary<Guid, Material> { [materialId] = material };
        var currentPrices = new Dictionary<Guid, MaterialPrice>();
        var warnings = new List<string>();

        // Act
        var result = MaterialCostHelper.CalculateMaterialCost(
            materialId,
            quantityPerHa: 10m,
            area: 2m,
            materialsDict,
            currentPrices,
            warnings);

        // Assert
        result.Should().BeNull();
        warnings.Should().ContainSingle()
            .Which.Should().Contain("Urea")
            .And.Contain(materialId.ToString());
    }

    public static TheoryData<decimal?> InvalidAmountPerMaterialData => new()
    {
        null,
        0m,
        -5m
    };

    [Theory]
    [MemberData(nameof(InvalidAmountPerMaterialData))]
    public void CalculateMaterialCost_NullZeroOrNegativeAmountPerMaterial_DefaultsToOne(decimal? amountPerMaterial)
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Rice Seed",
            Unit = "kg",
            AmmountPerMaterial = amountPerMaterial,
            IsPartition = false
        };
        var price = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            PricePerMaterial = 20_000m,
            ValidFrom = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var materialsDict = new Dictionary<Guid, Material> { [materialId] = material };
        var currentPrices = new Dictionary<Guid, MaterialPrice> { [materialId] = price };
        var warnings = new List<string>();

        // Act
        var result = MaterialCostHelper.CalculateMaterialCost(
            materialId,
            quantityPerHa: 12.5m,
            area: 2m,
            materialsDict,
            currentPrices,
            warnings);

        // Assert
        result.Should().NotBeNull();
        warnings.Should().BeEmpty();
        result!.AmountPerMaterial.Should().Be(1m);
        result.TotalQuantityNeeded.Should().Be(25m);
        result.PackagesNeeded.Should().Be(25m);
        result.ActualQuantity.Should().Be(25m);
        result.TotalCost.Should().Be(500_000m);
        result.CostPerHa.Should().Be(250_000m);
    }

    #endregion

    #region AggregateMaterialCost

    [Fact]
    public void AggregateMaterialCost_NewMaterialId_AddsNewEntryToAggregation()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var aggregation = new Dictionary<Guid, MaterialCostItem>();
        var item = new MaterialCostItem
        {
            MaterialId = materialId,
            MaterialName = "NPK Fertilizer",
            Unit = "kg",
            QuantityPerHa = 30m,
            TotalQuantityNeeded = 60m,
            AmountPerMaterial = 50m,
            PackagesNeeded = 2m,
            ActualQuantity = 100m,
            PricePerMaterial = 100_000m,
            TotalCost = 200_000m,
            CostPerHa = 100_000m,
            PriceValidFrom = validFrom
        };

        // Act
        MaterialCostHelper.AggregateMaterialCost(aggregation, item, effectiveArea: 2m);

        // Assert
        aggregation.Should().ContainKey(materialId);
        var added = aggregation[materialId];
        added.Should().NotBeSameAs(item);
        added.Should().BeEquivalentTo(item);
    }

    [Fact]
    public void AggregateMaterialCost_DuplicateMaterialId_SumsFieldsAndRecalculatesCostPerHa()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var aggregation = new Dictionary<Guid, MaterialCostItem>();

        var firstItem = new MaterialCostItem
        {
            MaterialId = materialId,
            MaterialName = "NPK Fertilizer",
            Unit = "kg",
            QuantityPerHa = 20m,
            TotalQuantityNeeded = 80m,
            AmountPerMaterial = 50m,
            PackagesNeeded = 2m,
            ActualQuantity = 100m,
            PricePerMaterial = 100_000m,
            TotalCost = 200_000m,
            CostPerHa = 50_000m,
            PriceValidFrom = validFrom
        };

        var secondItem = new MaterialCostItem
        {
            MaterialId = materialId,
            MaterialName = "NPK Fertilizer",
            Unit = "kg",
            QuantityPerHa = 15m,
            TotalQuantityNeeded = 60m,
            AmountPerMaterial = 50m,
            PackagesNeeded = 2m,
            ActualQuantity = 100m,
            PricePerMaterial = 100_000m,
            TotalCost = 200_000m,
            CostPerHa = 50_000m,
            PriceValidFrom = validFrom
        };

        // Act
        MaterialCostHelper.AggregateMaterialCost(aggregation, firstItem, effectiveArea: 4m);
        MaterialCostHelper.AggregateMaterialCost(aggregation, secondItem, effectiveArea: 4m);

        // Assert
        aggregation.Should().ContainSingle();
        var merged = aggregation[materialId];
        merged.MaterialId.Should().Be(materialId);
        merged.MaterialName.Should().Be("NPK Fertilizer");
        merged.Unit.Should().Be("kg");
        merged.AmountPerMaterial.Should().Be(50m);
        merged.PricePerMaterial.Should().Be(100_000m);
        merged.PriceValidFrom.Should().Be(validFrom);
        merged.QuantityPerHa.Should().Be(35m);
        merged.TotalQuantityNeeded.Should().Be(140m);
        merged.PackagesNeeded.Should().Be(4m);
        merged.ActualQuantity.Should().Be(200m);
        merged.TotalCost.Should().Be(400_000m);
        merged.CostPerHa.Should().Be(100_000m);
    }

    [Fact]
    public void AggregateMaterialCost_DistinctMaterialIds_KeepsEntriesSeparate()
    {
        // Arrange
        var aggregation = new Dictionary<Guid, MaterialCostItem>();
        var firstItem = new MaterialCostItem
        {
            MaterialId = Guid.NewGuid(),
            MaterialName = "NPK Fertilizer",
            Unit = "kg",
            QuantityPerHa = 20m,
            TotalQuantityNeeded = 40m,
            AmountPerMaterial = 50m,
            PackagesNeeded = 1m,
            ActualQuantity = 50m,
            PricePerMaterial = 100_000m,
            TotalCost = 100_000m,
            CostPerHa = 50_000m
        };
        var secondItem = new MaterialCostItem
        {
            MaterialId = Guid.NewGuid(),
            MaterialName = "Urea",
            Unit = "kg",
            QuantityPerHa = 10m,
            TotalQuantityNeeded = 20m,
            AmountPerMaterial = 25m,
            PackagesNeeded = 1m,
            ActualQuantity = 25m,
            PricePerMaterial = 80_000m,
            TotalCost = 80_000m,
            CostPerHa = 40_000m
        };

        // Act
        MaterialCostHelper.AggregateMaterialCost(aggregation, firstItem, effectiveArea: 2m);
        MaterialCostHelper.AggregateMaterialCost(aggregation, secondItem, effectiveArea: 2m);

        // Assert
        aggregation.Should().HaveCount(2);
        aggregation[firstItem.MaterialId].TotalCost.Should().Be(100_000m);
        aggregation[secondItem.MaterialId].TotalCost.Should().Be(80_000m);
    }

    #endregion
}
