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

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateCostByPlotAndStandardPlan;

public class CalculateCostByPlotAndStandardPlanQueryHandler :
    IRequestHandler<CalculateCostByPlotAndStandardPlanQuery, Result<CalculateMaterialsCostByAreaResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateCostByPlotAndStandardPlanQueryHandler> _logger;

    public CalculateCostByPlotAndStandardPlanQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<CalculateCostByPlotAndStandardPlanQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CalculateMaterialsCostByAreaResponse>> Handle(
        CalculateCostByPlotAndStandardPlanQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load Plot to get area
            var plot = await _unitOfWork.Repository<Plot>().FindAsync(
                match: p => p.Id == request.PlotId
            );

            if (plot == null)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure(
                    $"Plot with ID {request.PlotId} not found.",
                    "PlotNotFound");
            }

            if (plot.Area <= 0)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure(
                    "Plot area is not valid or is zero.",
                    "InvalidPlotArea");
            }

            // Determine source: StandardPlan or manual input
            List<TaskWithMaterialsInput> tasks;
            string sourceDescription;

            if (request.StandardPlanId.HasValue)
            {
                // Load from StandardPlan
                var standardPlan = await _unitOfWork.Repository<StandardPlan>().FindAsync(
                    match: sp => sp.Id == request.StandardPlanId.Value,
                    includeProperties: q => q
                        .Include(sp => sp.StandardPlanStages)
                            .ThenInclude(s => s.StandardPlanTasks)
                                .ThenInclude(t => t.StandardPlanTaskMaterials)
                                    .ThenInclude(tm => tm.Material)
                        .Include(sp => sp.SeedServices)
                            .ThenInclude(ss => ss.Material)
                );

                if (standardPlan == null)
                {
                    return Result<CalculateMaterialsCostByAreaResponse>.Failure(
                        $"StandardPlan with ID {request.StandardPlanId.Value} not found.",
                        "StandardPlanNotFound");
                }

                // Convert StandardPlan to input models
                tasks = new List<TaskWithMaterialsInput>();

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

                seedServices = standardPlan.SeedServices
                    .Where(ss => ss.MaterialId.HasValue)
                    .Select(ss => new SeedServiceInput
                    {
                        MaterialId = ss.MaterialId!.Value,
                        QuantityPerHa = ss.ActualQuantity,
                        Notes = ss.Notes
                    })
                    .ToList();

                sourceDescription = $"StandardPlan '{standardPlan.PlanName}'";
            }
            else
            {
                // Use manual input
                tasks = request.Tasks ?? new List<TaskWithMaterialsInput>();
                seedServices = request.SeedServices ?? new List<SeedServiceInput>();
                sourceDescription = "manual input";
            }

            // Now perform the calculation using the common logic
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
            var materialCostItems = new List<MaterialCostItem>();
            var taskCostBreakdowns = new List<TaskCostBreakdown>();
            var seedServiceCostBreakdowns = new List<SeedServiceCostBreakdown>();
            var materialAggregation = new Dictionary<Guid, MaterialCostItem>();

            // Process Tasks
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
                    var materialCost = CalculateMaterialCost(
                        materialInput.MaterialId,
                        materialInput.QuantityPerHa,
                        plot.Area,
                        materialsDict,
                        currentPrices,
                        priceWarnings);

                    if (materialCost != null)
                    {
                        taskBreakdown.Materials.Add(materialCost);
                        taskTotalCost += materialCost.TotalCost;

                        if (!materialAggregation.ContainsKey(materialInput.MaterialId))
                        {
                            materialAggregation[materialInput.MaterialId] = materialCost;
                        }
                        else
                        {
                            var existing = materialAggregation[materialInput.MaterialId];
                            existing.TotalQuantityNeeded += materialCost.TotalQuantityNeeded;
                            existing.PackagesNeeded += materialCost.PackagesNeeded;
                            existing.ActualQuantity += materialCost.ActualQuantity;
                            existing.TotalCost += materialCost.TotalCost;
                            existing.CostPerHa = existing.TotalCost / plot.Area;
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
                    plot.Area,
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

                    if (!materialAggregation.ContainsKey(seedServiceInput.MaterialId))
                    {
                        materialAggregation[seedServiceInput.MaterialId] = materialCost;
                    }
                    else
                    {
                        var existing = materialAggregation[seedServiceInput.MaterialId];
                        existing.TotalQuantityNeeded += materialCost.TotalQuantityNeeded;
                        existing.PackagesNeeded += materialCost.PackagesNeeded;
                        existing.ActualQuantity += materialCost.ActualQuantity;
                        existing.TotalCost += materialCost.TotalCost;
                        existing.CostPerHa = existing.TotalCost / plot.Area;
                    }
                }
            }

            var totalCostForArea = totalTaskMaterialsCost + totalSeedServicesCost;
            var totalCostPerHa = totalCostForArea / plot.Area;

            materialCostItems = materialAggregation.Values.ToList();

            if (!materialCostItems.Any() && !taskCostBreakdowns.Any() && !seedServiceCostBreakdowns.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure(
                    "No valid material cost calculations could be performed.",
                    "NoValidCalculations");
            }

            var response = new CalculateMaterialsCostByAreaResponse
            {
                Area = plot.Area,
                TotalCostPerHa = totalCostPerHa,
                TotalCostForArea = totalCostForArea,
                TotalTaskMaterialsCost = totalTaskMaterialsCost,
                MaterialCostItems = materialCostItems,
                TaskCostBreakdowns = taskCostBreakdowns,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any()
                ? $"Successfully calculated material costs for Plot (using {sourceDescription}) with {priceWarnings.Count} warning(s)."
                : $"Successfully calculated material costs for Plot (using {sourceDescription}).";

            _logger.LogInformation(
                "Calculated material costs for Plot {PlotId} (Area: {Area}ha) using {Source}. Total cost: {TotalCost}",
                request.PlotId, plot.Area, sourceDescription, totalCostForArea);

            return Result<CalculateMaterialsCostByAreaResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error calculating material costs for Plot {PlotId}",
                request.PlotId);
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