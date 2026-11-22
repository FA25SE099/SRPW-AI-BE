using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateGroupMaterialCost;

public class CalculateGroupMaterialCostQueryHandler : IRequestHandler<CalculateGroupMaterialCostQuery, Result<CalculateGroupMaterialCostResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CalculateGroupMaterialCostQueryHandler> _logger;

    public CalculateGroupMaterialCostQueryHandler(IApplicationDbContext context, ILogger<CalculateGroupMaterialCostQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<CalculateGroupMaterialCostResponse>> Handle(CalculateGroupMaterialCostQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.UtcNow;

            var group = await _context.Groups
                .AsNoTracking()
                .Where(g => g.Id == request.GroupId)
                .Select(g => new GroupCostInfo(
                    g.TotalArea,
                    g.GroupPlots
                        .Select(gp => new PlotCostInfo(gp.Plot.Id, gp.Plot.Area, gp.Plot.SoThua, gp.Plot.SoTo))
                        .ToList()))
                .FirstOrDefaultAsync(cancellationToken);

            if (group == null)
            {
                return Result<CalculateGroupMaterialCostResponse>.Failure($"Group with ID {request.GroupId} not found.", "GroupNotFound");
            }

            if (group.TotalArea == null || group.TotalArea.Value <= 0)
            {
                return Result<CalculateGroupMaterialCostResponse>.Failure("Group's Total Area is not defined or is zero.", "GroupAreaMissing");
            }

            decimal effectiveTotalArea = group.TotalArea.Value;

            var allMaterialIds = request.Materials
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var materialDetails = await _context.Materials
                .AsNoTracking()
                .Where(m => allMaterialIds.Contains(m.Id))
                .ToListAsync(cancellationToken);
            var materialDetailsMap = materialDetails.ToDictionary(m => m.Id, m => m);

            var materialPriceMap = await MaterialCostHelper.GetCurrentPricesAsync(_context, allMaterialIds, today, cancellationToken);

            decimal totalGroupCost = 0M;
            var priceWarnings = new List<string>();
            var materialCostDetails = new List<MaterialCostDetailResponse>();
            var materialAggregation = new Dictionary<Guid, (decimal TotalQuantity, decimal TotalPackages, decimal TotalCost)>();

            foreach (var materialInput in request.Materials)
            {
                var (materialCost, aggregatedData) = CalculateMaterialCostForGroup(
                    materialInput.MaterialId,
                    materialInput.Quantity,
                    effectiveTotalArea,
                    materialDetailsMap,
                    materialPriceMap,
                    priceWarnings);

                if (materialCost == null) continue;

                materialCostDetails.Add(materialCost);
                totalGroupCost += materialCost.MaterialTotalCost;

                materialAggregation.TryGetValue(materialInput.MaterialId, out var current);
                materialAggregation[materialInput.MaterialId] = (
                    current.TotalQuantity + aggregatedData.TotalQuantity,
                    current.TotalPackages + aggregatedData.TotalPackages,
                    current.TotalCost + aggregatedData.TotalCost
                );
            }

            var plotCostDetails = BuildPlotCostDetails(group.Plots, effectiveTotalArea, totalGroupCost);
            AppendAggregatedMaterialCosts(materialCostDetails, materialAggregation, materialDetailsMap, materialPriceMap);

            var response = new CalculateGroupMaterialCostResponse
            {
                GroupId = request.GroupId,
                TotalGroupArea = effectiveTotalArea,
                TotalGroupCost = totalGroupCost,
                MaterialCostDetails = materialCostDetails,
                PlotCostDetails = plotCostDetails,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any()
                ? $"Successfully calculated cost with {priceWarnings.Count} warning(s)."
                : "Successfully calculated group material cost.";

            _logger.LogInformation("Calculated total group cost for Group ID {GId}: {Cost}. Plots analyzed: {PlotCount}",
                request.GroupId, totalGroupCost, plotCostDetails.Count);

            return Result<CalculateGroupMaterialCostResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating group material cost for Group ID {GId}", request.GroupId);
            return Result<CalculateGroupMaterialCostResponse>.Failure("An error occurred during cost calculation.", "CostCalculationFailed");
        }
    }

    private sealed record PlotCostInfo(Guid Id, decimal Area, int? SoThua, int? SoTo);
    private sealed record GroupCostInfo(decimal? TotalArea, List<PlotCostInfo> Plots);

    private static List<PlotCostDetailResponse> BuildPlotCostDetails(
        IEnumerable<PlotCostInfo> groupPlots,
        decimal effectiveTotalArea,
        decimal totalGroupCost)
    {
        var plotCostDetails = new List<PlotCostDetailResponse>();

        foreach (var groupPlot in groupPlots)
        {
            var plot = groupPlot;
            var plotArea = plot.Area;
            if (plotArea <= 0) continue;

            var plotRatio = plotArea / effectiveTotalArea;
            plotCostDetails.Add(new PlotCostDetailResponse
            {
                PlotId = plot.Id,
                PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                PlotArea = plotArea,
                AreaRatio = plotRatio,
                AllocatedCost = totalGroupCost * plotRatio
            });
        }

        return plotCostDetails;
    }

    private static void AppendAggregatedMaterialCosts(
        List<MaterialCostDetailResponse> materialCostDetails,
        Dictionary<Guid, (decimal TotalQuantity, decimal TotalPackages, decimal TotalCost)> materialAggregation,
        Dictionary<Guid, Material> materialDetailsMap,
        Dictionary<Guid, MaterialPrice> materialPriceMap)
    {
        foreach (var kvp in materialAggregation)
        {
            if (!materialDetailsMap.TryGetValue(kvp.Key, out var materialDetail)) continue;
            if (!materialPriceMap.TryGetValue(kvp.Key, out var priceInfo)) continue;

            materialCostDetails.Add(new MaterialCostDetailResponse
            {
                MaterialId = kvp.Key,
                MaterialName = materialDetail.Name,
                Unit = materialDetail.Unit,
                RequiredQuantity = kvp.Value.TotalQuantity,
                PackagesNeeded = kvp.Value.TotalPackages,
                EffectivePricePerPackage = priceInfo.PricePerMaterial,
                MaterialTotalCost = kvp.Value.TotalCost,
                PriceValidFrom = priceInfo.ValidFrom
            });
        }
    }

    private static (MaterialCostDetailResponse? materialCost, (decimal TotalQuantity, decimal TotalPackages, decimal TotalCost) aggregatedData)
        CalculateMaterialCostForGroup(
            Guid materialId,
            decimal quantityPerHa,
            decimal totalArea,
            Dictionary<Guid, Material> materialDetailsMap,
            Dictionary<Guid, MaterialPrice> materialPriceMap,
            List<string> priceWarnings)
    {
        if (!materialDetailsMap.TryGetValue(materialId, out var materialDetail))
        {
            priceWarnings.Add($"Material ID {materialId} not found.");
            return (null, (0, 0, 0));
        }

        if (!materialPriceMap.TryGetValue(materialId, out var priceInfo))
        {
            priceWarnings.Add($"No valid price found for material '{materialDetail.Name}' (ID: {materialId}).");
            return (null, (0, 0, 0));
        }

        var amountPerPackage = materialDetail.AmmountPerMaterial is > 0 ? materialDetail.AmmountPerMaterial.Value : 1M;
        var requiredQuantity = quantityPerHa * totalArea;
        var packagesNeeded = MaterialCostHelper.CalculatePackagesNeeded(requiredQuantity, amountPerPackage, materialDetail.IsPartition);
        var totalCost = packagesNeeded * priceInfo.PricePerMaterial;

        var materialCost = new MaterialCostDetailResponse
        {
            MaterialId = materialId,
            MaterialName = materialDetail.Name,
            Unit = materialDetail.Unit,
            RequiredQuantity = requiredQuantity,
            PackagesNeeded = packagesNeeded,
            EffectivePricePerPackage = priceInfo.PricePerMaterial,
            MaterialTotalCost = totalCost,
            PriceValidFrom = priceInfo.ValidFrom
        };

        return (materialCost, (requiredQuantity, packagesNeeded, totalCost));
    }
}