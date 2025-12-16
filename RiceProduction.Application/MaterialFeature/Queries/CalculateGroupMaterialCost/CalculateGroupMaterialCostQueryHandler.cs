using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.MaterialFeature.Queries.CalculateGroupMaterialCost;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

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

            // --- 2. Collect all material IDs from both tasks and seed services ---
            var taskMaterialIds = request.Tasks
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()




                .ToList();

            var seedServiceMaterialIds = request.SeedServices
                .Select(s => s.MaterialId)
                .Distinct()
                .ToList();

            var allMaterialIds = taskMaterialIds.Union(seedServiceMaterialIds).ToList();

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
            decimal totalTaskMaterialsCost = 0M;
            decimal totalSeedServicesCost = 0M;
            var priceWarnings = new List<string>();
            var plotCostDetails = new List<PlotCostDetailResponse>();

            // Dictionary to aggregate all materials for backward compatibility
            var materialAggregation = new Dictionary<Guid, (decimal TotalQuantity, decimal TotalPackages, decimal TotalCost)>();

            // Task-specific tracking
            var taskCostBreakdowns = new List<TaskCostBreakdown>();

            // Seed service tracking
            var seedServiceCostBreakdowns = new List<SeedServiceCostBreakdown>();

            // --- 5. Process Tasks with Materials ---
            foreach (var taskInput in request.Tasks)
            {
                var taskBreakdown = new TaskCostBreakdown
                {
                    TaskName = taskInput.TaskName,
                    TaskDescription = taskInput.TaskDescription
                };

                decimal taskTotalCost = 0M;

                foreach (var materialInput in taskInput.Materials)

                {
                    var (materialCost, aggregatedData) = CalculateMaterialCostForGroup(
                        materialInput.MaterialId,
                        materialInput.QuantityPerHa,
                        effectiveTotalArea,
                        materialDetailsMap,
                        materialPriceMap,
                        priceWarnings);











                    if (materialCost != null)
                    {
                        taskBreakdown.Materials.Add(materialCost);
                        taskTotalCost += materialCost.MaterialTotalCost;

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

                taskBreakdown.TotalTaskCost = taskTotalCost;
                taskCostBreakdowns.Add(taskBreakdown);
                totalTaskMaterialsCost += taskTotalCost;
            }

            // --- 6. Process Seed Services ---
            foreach (var seedServiceInput in request.SeedServices)
            {
                var (seedServiceCost, aggregatedData) = CalculateMaterialCostForGroup(
                    seedServiceInput.MaterialId,
                    seedServiceInput.QuantityPerHa,
                    effectiveTotalArea,
                    materialDetailsMap,
                    materialPriceMap,
                    priceWarnings);

                if (seedServiceCost != null)
                {
                    var seedServiceBreakdown = new SeedServiceCostBreakdown
                    {
                        MaterialId = seedServiceInput.MaterialId,
                        MaterialName = seedServiceCost.MaterialName,
                        Unit = seedServiceCost.Unit,
                        QuantityPerHa = seedServiceInput.QuantityPerHa,
                        RequiredQuantity = seedServiceCost.RequiredQuantity,
                        PackagesNeeded = seedServiceCost.PackagesNeeded,
                        EffectivePricePerPackage = seedServiceCost.EffectivePricePerPackage,
                        TotalCost = seedServiceCost.MaterialTotalCost,
                        Notes = seedServiceInput.Notes,
                        PriceValidFrom = seedServiceCost.PriceValidFrom
                    };

                    seedServiceCostBreakdowns.Add(seedServiceBreakdown);
                    totalSeedServicesCost += seedServiceCost.MaterialTotalCost;

                    // Aggregate for overall summary
                    if (!materialAggregation.ContainsKey(seedServiceInput.MaterialId))
                    {
                        materialAggregation[seedServiceInput.MaterialId] = (0M, 0M, 0M);
                    }
                    var current = materialAggregation[seedServiceInput.MaterialId];
                    materialAggregation[seedServiceInput.MaterialId] = (
                        current.TotalQuantity + aggregatedData.TotalQuantity,
                        current.TotalPackages + aggregatedData.TotalPackages,
                        current.TotalCost + aggregatedData.TotalCost

                    );
                }
            }

            // --- 7. Calculate total cost ---
            totalGroupCost = totalTaskMaterialsCost + totalSeedServicesCost;

            // --- 8. Calculate plot cost details (proportional allocation) ---
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

            // --- 9. Create aggregated material cost details (backward compatibility) ---
            var materialCostDetails = new List<MaterialCostDetailResponse>();

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

            // --- 10. Build response ---

            var response = new CalculateGroupMaterialCostResponse
            {
                GroupId = request.GroupId,
                TotalGroupArea = effectiveTotalArea,
                TotalGroupCost = totalGroupCost,
                TotalTaskMaterialsCost = totalTaskMaterialsCost,
                TotalSeedServicesCost = totalSeedServicesCost,
                MaterialCostDetails = materialCostDetails,
                TaskCostBreakdowns = taskCostBreakdowns,
                SeedServiceCostBreakdowns = seedServiceCostBreakdowns,
                PlotCostDetails = plotCostDetails,
                PriceWarnings = priceWarnings
            };

@@ -184,8 + 241,8 @@ public class CalculateGroupMaterialCostQueryHandler : IRequestHandler<CalculateG
                ? $"Successfully calculated cost with {priceWarnings.Count} warning(s)." 
                : "Successfully calculated group material cost.";

            _logger.LogInformation("Calculated total group cost for Group ID {GId}: {Cost} (Tasks: {TaskCost}, Seeds: {SeedCost}). Plots analyzed: {PlotCount}", 
                request.GroupId, totalGroupCost, totalTaskMaterialsCost, totalSeedServicesCost, plotCostDetails.Count);

            return Result<CalculateGroupMaterialCostResponse>.Success(response, message);
        }

@@ -195,4 +252,54 @@ public class CalculateGroupMaterialCostQueryHandler : IRequestHandler<CalculateG
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