using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Linq;
using System;
using System.Collections.Generic;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateGroupMaterialCost;

public class CalculateGroupMaterialCostQueryHandler : IRequestHandler<CalculateGroupMaterialCostQuery, Result<CalculateGroupMaterialCostResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateGroupMaterialCostQueryHandler> _logger;

    public CalculateGroupMaterialCostQueryHandler(IUnitOfWork unitOfWork, ILogger<CalculateGroupMaterialCostQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CalculateGroupMaterialCostResponse>> Handle(CalculateGroupMaterialCostQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.UtcNow;

            // --- 1. Tải Group và Plots ---
            var group = await _unitOfWork.Repository<Group>().FindAsync(
                match: g => g.Id == request.GroupId,
                includeProperties: q => q.Include(g => g.GroupPlots).ThenInclude(gp => gp.Plot)
            );

            if (group == null)
            {
                return Result<CalculateGroupMaterialCostResponse>.Failure($"Group with ID {request.GroupId} not found.", "GroupNotFound");
            }

            if (group.TotalArea == null || group.TotalArea.Value <= 0)
            {
                return Result<CalculateGroupMaterialCostResponse>.Failure("Group's Total Area is not defined or is zero.", "GroupAreaMissing");
            }

            decimal effectiveTotalArea = group.TotalArea.Value;

            // --- 2. Collect all material IDs from the Materials list ---
            var allMaterialIds = request.Materials
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            // --- 3. Tải chi tiết Material và Material Prices ---
            var materialDetails = await _unitOfWork.Repository<Material>()
                .ListAsync(filter: m => allMaterialIds.Contains(m.Id));

            var materialDetailsMap = materialDetails.ToDictionary(m => m.Id, m => m);

            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => allMaterialIds.Contains(p.MaterialId) && p.ValidFrom <= today
            );

            var materialPriceMap = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p);

            // --- 4. Initialize tracking dictionaries ---
            decimal totalGroupCost = 0M;
            var priceWarnings = new List<string>();
            var plotCostDetails = new List<PlotCostDetailResponse>();

            // Dictionary to aggregate all materials
            var materialAggregation = new Dictionary<Guid, (decimal TotalQuantity, decimal TotalPackages, decimal TotalCost)>();

            // --- 5. Process Materials ---
            var materialCostDetails = new List<MaterialCostDetailResponse>();
            
            foreach (var materialInput in request.Materials)
            {
                var (materialCost, aggregatedData) = CalculateMaterialCostForGroup(
                    materialInput.MaterialId,
                    materialInput.Quantity,
                    effectiveTotalArea,
                    materialDetailsMap,
                    materialPriceMap,
                    priceWarnings);

                if (materialCost != null)
                {
                    materialCostDetails.Add(materialCost);
                    totalGroupCost += materialCost.MaterialTotalCost;

                    // Aggregate for overall summary
                    if (!materialAggregation.ContainsKey(materialInput.MaterialId))
                    {
                        materialAggregation[materialInput.MaterialId] = (0M, 0M, 0M);
                    }
                    var current = materialAggregation[materialInput.MaterialId];
                    materialAggregation[materialInput.MaterialId] = (
                        current.TotalQuantity + aggregatedData.TotalQuantity,
                        current.TotalPackages + aggregatedData.TotalPackages,
                        current.TotalCost + aggregatedData.TotalCost
                    );
                }
            }

            // --- 6. Calculate plot cost details (proportional allocation) ---
            foreach (var groupPlot in group.GroupPlots)
            {
                var plot = groupPlot.Plot;
                var plotArea = plot.Area;
                if (plotArea <= 0) continue;

                var plotRatio = plotArea / effectiveTotalArea;
                var allocatedCost = totalGroupCost * plotRatio;

                plotCostDetails.Add(new PlotCostDetailResponse
                {
                    PlotId = plot.Id,
                    PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                    PlotArea = plotArea,
                    AreaRatio = plotRatio,
                    AllocatedCost = allocatedCost
                });
            }

            // --- 7. Create aggregated material cost details ---
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

            // --- 8. Build response ---
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

    private (MaterialCostDetailResponse? materialCost, (decimal TotalQuantity, decimal TotalPackages, decimal TotalCost) aggregatedData)
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

        var amountPerPackage = materialDetail.AmmountPerMaterial.GetValueOrDefault(1M);
        if (amountPerPackage <= 0) amountPerPackage = 1M;

        // Calculate total quantity needed for entire group area
        var requiredQuantity = quantityPerHa * totalArea;

        // Calculate packages needed
        var packagesNeeded = materialDetail.IsPartition
            ? requiredQuantity / amountPerPackage
            : Math.Ceiling(requiredQuantity / amountPerPackage);

        // Calculate total cost
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