using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialCostCalculationRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateCostByStandardPlanAndArea;

public class CalculateCostByStandardPlanAndAreaQueryHandler :
    IRequestHandler<CalculateCostByStandardPlanAndAreaQuery, Result<CalculateMaterialsCostByAreaResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateCostByStandardPlanAndAreaQueryHandler> _logger;

    public CalculateCostByStandardPlanAndAreaQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<CalculateCostByStandardPlanAndAreaQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CalculateMaterialsCostByAreaResponse>> Handle(
        CalculateCostByStandardPlanAndAreaQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load StandardPlan with all tasks, materials, AND seed services
            var standardPlan = await _unitOfWork.Repository<StandardPlan>().FindAsync(
                match: sp => sp.Id == request.StandardPlanId,
                includeProperties: q => q
                    .Include(sp => sp.StandardPlanStages)
                        .ThenInclude(s => s.StandardPlanTasks)
                            .ThenInclude(t => t.StandardPlanTaskMaterials)
                                .ThenInclude(tm => tm.Material)
                    .Include(sp => sp.SeedServices) // ? Load Seed Services
                        .ThenInclude(ss => ss.Material)
            );

            if (standardPlan == null)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure(
                    $"StandardPlan with ID {request.StandardPlanId} not found.",
                    "StandardPlanNotFound");
            }

            // Convert StandardPlan tasks to TaskWithMaterialsInput
            var tasks = new List<TaskWithMaterialsInput>();

            foreach (var stage in standardPlan.StandardPlanStages.OrderBy(s => s.SequenceOrder))
            {
                foreach (var task in stage.StandardPlanTasks.OrderBy(t => t.SequenceOrder))
                {
                    var taskInput = new TaskWithMaterialsInput
                    {
                        TaskName = task.TaskName,
                        TaskDescription = task.Description,
                        Materials = task.StandardPlanTaskMaterials.Select(tm => new TaskMaterialInput
                        {
                            MaterialId = tm.MaterialId,
                            QuantityPerHa = tm.QuantityPerHa
                        }).ToList()
                    };

                    tasks.Add(taskInput);
                }
            }

            // Convert StandardPlan seed services to SeedServiceInput
            var seedServices = standardPlan.SeedServices
                .Where(ss => ss.MaterialId.HasValue)
                .Select(ss => new SeedServiceInput
                {
                    MaterialId = ss.MaterialId!.Value,
                    QuantityPerHa = ss.ActualQuantity,
                    Notes = ss.Notes
                })
                .ToList();

            // Now use the existing calculation logic
            var currentDate = DateTime.UtcNow;
            var priceWarnings = new List<string>();

            // Collect all material IDs
            var taskMaterialIds = tasks
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var seedServiceMaterialIds = seedServices
                .Select(s => s.MaterialId)
                .Distinct()
                .ToList();

            var allMaterialIds = taskMaterialIds.Union(seedServiceMaterialIds).ToList();

            // Load material details
            var materials = await _unitOfWork.Repository<Material>().ListAsync(
                filter: m => allMaterialIds.Contains(m.Id) && m.IsActive
            );

            if (!materials.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure(
                    "No active materials found.",
                    "MaterialsNotFound");
            }

            var materialsDict = materials.ToDictionary(m => m.Id, m => m);

            // Load current prices
            var allPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => allMaterialIds.Contains(p.MaterialId) && p.ValidFrom <= currentDate
            );

            var currentPrices = allPrices
                .Where(p => !p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date)
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p);

            // Calculate costs
            decimal totalTaskMaterialsCost = 0M;
            decimal totalSeedServicesCost = 0M;
            var taskCostBreakdowns = new List<TaskCostBreakdown>();
            var seedServiceCostBreakdowns = new List<SeedServiceCostBreakdown>();

            // ? FIX: Aggregation dictionary for MaterialCostItems ONLY
            var materialAggregation = new Dictionary<Guid, MaterialCostItem>();

            // ? FIX: Process Tasks WITHOUT polluting materialAggregation
            foreach (var taskInput in tasks)
            {
                var taskBreakdown = new TaskCostBreakdown
                {
                    TaskName = taskInput.TaskName,
                    TaskDescription = taskInput.TaskDescription
                };

                decimal taskTotalCost = 0M;

                foreach (var materialInput in taskInput.Materials)
                {
                    // Calculate material cost for THIS task ONLY
                    var materialCost = CalculateMaterialCost(
                        materialInput.MaterialId,
                        materialInput.QuantityPerHa,
                        request.Area,
                        materialsDict,
                        currentPrices,
                        priceWarnings);

                    if (materialCost != null)
                    {
                        // ? Add to task breakdown (this is correct)
                        taskBreakdown.Materials.Add(materialCost);
                        taskTotalCost += materialCost.TotalCost;

                        // ? FIX: Aggregate for MaterialCostItems (global summary)
                        if (!materialAggregation.ContainsKey(materialInput.MaterialId))
                        {
                            // First occurrence: clone the cost item
                            materialAggregation[materialInput.MaterialId] = new MaterialCostItem
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
                        }
                        else
                        {
                            // Subsequent occurrences: aggregate
                            var existing = materialAggregation[materialInput.MaterialId];
                            existing.TotalQuantityNeeded += materialCost.TotalQuantityNeeded;
                            existing.PackagesNeeded += materialCost.PackagesNeeded;
                            existing.ActualQuantity += materialCost.ActualQuantity;
                            existing.TotalCost += materialCost.TotalCost;
                            existing.CostPerHa = existing.TotalCost / request.Area;
                        }
                    }
                }

                taskBreakdown.TotalTaskCost = taskTotalCost;
                taskCostBreakdowns.Add(taskBreakdown);
                totalTaskMaterialsCost += taskTotalCost;
            }

            // Process Seed Services
            foreach (var seedServiceInput in seedServices)
            {
                var materialCost = CalculateMaterialCost(
                    seedServiceInput.MaterialId,
                    seedServiceInput.QuantityPerHa,
                    request.Area,
                    materialsDict,
                    currentPrices,
                    priceWarnings);

                if (materialCost != null)
                {
                    var seedServiceBreakdown = new SeedServiceCostBreakdown
                    {
                        MaterialId = seedServiceInput.MaterialId,
                        MaterialName = materialCost.MaterialName,
                        Unit = materialCost.Unit,
                        QuantityPerHa = seedServiceInput.QuantityPerHa,
                        RequiredQuantity = materialCost.TotalQuantityNeeded,
                        PackagesNeeded = materialCost.PackagesNeeded,
                        EffectivePricePerPackage = materialCost.PricePerMaterial,
                        TotalCost = materialCost.TotalCost,
                        Notes = seedServiceInput.Notes,
                        PriceValidFrom = materialCost.PriceValidFrom
                    };

                    seedServiceCostBreakdowns.Add(seedServiceBreakdown);
                    totalSeedServicesCost += materialCost.TotalCost;

                    // Aggregate seed services too
                    if (!materialAggregation.ContainsKey(seedServiceInput.MaterialId))
                    {
                        materialAggregation[seedServiceInput.MaterialId] = new MaterialCostItem
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
                    }
                    else
                    {
                        var existing = materialAggregation[seedServiceInput.MaterialId];
                        existing.TotalQuantityNeeded += materialCost.TotalQuantityNeeded;
                        existing.PackagesNeeded += materialCost.PackagesNeeded;
                        existing.ActualQuantity += materialCost.ActualQuantity;
                        existing.TotalCost += materialCost.TotalCost;
                        existing.CostPerHa = existing.TotalCost / request.Area;
                    }
                }
            }

            var totalCostForArea = totalTaskMaterialsCost + totalSeedServicesCost;
            var totalCostPerHa = totalCostForArea / request.Area;

            var materialCostItems = materialAggregation.Values.ToList();

            if (!materialCostItems.Any() && !taskCostBreakdowns.Any() && !seedServiceCostBreakdowns.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure(
                    "No valid material cost calculations could be performed.",
                    "NoValidCalculations");
            }

            // Sort seed services by total cost (descending - highest first)
            seedServiceCostBreakdowns = seedServiceCostBreakdowns
                .OrderByDescending(ss => ss.TotalCost)
                .ToList();

            var response = new CalculateMaterialsCostByAreaResponse
            {
                Area = request.Area,
                TotalCostPerHa = totalCostPerHa,
                TotalCostForArea = totalCostForArea,
                TotalTaskMaterialsCost = totalTaskMaterialsCost,
                TotalSeedServicesCost = totalSeedServicesCost,
                MaterialCostItems = materialCostItems,
                TaskCostBreakdowns = taskCostBreakdowns,
                SeedServiceCostBreakdowns = seedServiceCostBreakdowns,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any()
                ? $"Successfully calculated material costs from StandardPlan with {priceWarnings.Count} warning(s)."
                : $"Successfully calculated material costs from StandardPlan '{standardPlan.PlanName}'.";

            _logger.LogInformation(
                "Calculated material costs from StandardPlan {StandardPlanId} for area {Area}ha. Total cost: {TotalCost}",
                request.StandardPlanId, request.Area, totalCostForArea);

            return Result<CalculateMaterialsCostByAreaResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error calculating material costs from StandardPlan {StandardPlanId} for area {Area}ha",
                request.StandardPlanId, request.Area);
            return Result<CalculateMaterialsCostByAreaResponse>.Failure(
                "An error occurred during cost calculation.",
                "CostCalculationFailed");
        }
    }

    private MaterialCostItem? CalculateMaterialCost(
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

        var amountPerMaterial = material.AmmountPerMaterial.GetValueOrDefault(1M);
        if (amountPerMaterial <= 0) amountPerMaterial = 1M;

        var totalQuantityNeeded = quantityPerHa * area;
        var packagesNeeded = material.IsPartition
            ? totalQuantityNeeded / amountPerMaterial
            : Math.Ceiling(totalQuantityNeeded / amountPerMaterial);
        var actualQuantity = packagesNeeded * amountPerMaterial;
        var totalCost = packagesNeeded * priceInfo.PricePerMaterial;
        var costPerHa = totalCost / area;

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
            CostPerHa = costPerHa,
            PriceValidFrom = priceInfo.ValidFrom
        };
    }
}