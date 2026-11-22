using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Helpers;

public static class MaterialCostHelper
{
    public static async Task<Dictionary<Guid, MaterialPrice>> GetCurrentPricesAsync(
        IUnitOfWork unitOfWork,
        List<Guid> materialIds,
        DateTime currentDate)
    {
        var allPrices = await unitOfWork.Repository<MaterialPrice>().ListAsync(
            filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom <= currentDate);

        return allPrices
            .Where(p => !p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date)
            .GroupBy(p => p.MaterialId)
            .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
            .ToDictionary(p => p.MaterialId, p => p);
    }

    public static decimal CalculatePackagesNeeded(decimal totalQuantityNeeded, decimal amountPerMaterial, bool isPartition) =>
        isPartition
            ? totalQuantityNeeded / amountPerMaterial
            : Math.Ceiling(totalQuantityNeeded / amountPerMaterial);

    public static MaterialCostItem? CalculateMaterialCost(
        Guid materialId,
        decimal quantityPerHa,
        decimal area,
        Dictionary<Guid, Material> materialsDict,
        Dictionary<Guid, MaterialPrice> currentPrices,
        List<string> priceWarnings)
    {
        if (!materialsDict.TryGetValue(materialId, out var material))
        {
            priceWarnings.Add($"Material ID {materialId} not found or is inactive.");
            return null;
        }

        if (!currentPrices.TryGetValue(materialId, out var priceInfo))
        {
            priceWarnings.Add($"No valid price found for material '{material.Name}' (ID: {material.Id}).");
            return null;
        }

        var amountPerMaterial = material.EffectiveAmountPerMaterial;
        var totalQuantityNeeded = quantityPerHa * area;
        var packagesNeeded = material.CalculatePackagesNeeded(totalQuantityNeeded);
        var actualQuantity = packagesNeeded * amountPerMaterial;
        var totalCost = packagesNeeded * priceInfo.PricePerMaterial;

        return new MaterialCostItem
        {
            MaterialId = material.Id,
            MaterialName = material.Name,
            Unit = material.Unit,
            QuantityPerHa = quantityPerHa,
            TotalQuantityNeeded = totalQuantityNeeded,
            AmountPerMaterial = amountPerMaterial,
            PackagesNeeded = packagesNeeded,
            ActualQuantity = actualQuantity,
            PricePerMaterial = priceInfo.PricePerMaterial,
            TotalCost = totalCost,
            CostPerHa = totalCost / area,
            PriceValidFrom = priceInfo.ValidFrom
        };
    }

    public static void AggregateMaterialCost(
        Dictionary<Guid, MaterialCostItem> aggregation,
        MaterialCostItem materialCost,
        decimal effectiveArea)
    {
        if (!aggregation.TryGetValue(materialCost.MaterialId, out var existing))
        {
            aggregation[materialCost.MaterialId] = new MaterialCostItem
            {
                MaterialId = materialCost.MaterialId,
                MaterialName = materialCost.MaterialName,
                Unit = materialCost.Unit,
                QuantityPerHa = materialCost.QuantityPerHa,
                TotalQuantityNeeded = materialCost.TotalQuantityNeeded,
                AmountPerMaterial = materialCost.AmountPerMaterial,
                PackagesNeeded = materialCost.PackagesNeeded,
                ActualQuantity = materialCost.ActualQuantity,
                PricePerMaterial = materialCost.PricePerMaterial,
                TotalCost = materialCost.TotalCost,
                CostPerHa = materialCost.CostPerHa,
                PriceValidFrom = materialCost.PriceValidFrom
            };
            return;
        }

        existing.QuantityPerHa += materialCost.QuantityPerHa;
        existing.TotalQuantityNeeded += materialCost.TotalQuantityNeeded;
        existing.PackagesNeeded += materialCost.PackagesNeeded;
        existing.ActualQuantity += materialCost.ActualQuantity;
        existing.TotalCost += materialCost.TotalCost;
        existing.CostPerHa = existing.TotalCost / effectiveArea;
    }
}
