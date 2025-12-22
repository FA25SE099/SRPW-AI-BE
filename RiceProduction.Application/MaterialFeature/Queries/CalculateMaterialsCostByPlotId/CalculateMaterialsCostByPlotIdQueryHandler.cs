using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByPlotId;

public class CalculateMaterialsCostByPlotIdQueryHandler : IRequestHandler<CalculateMaterialsCostByPlotIdQuery, Result<CalculateMaterialsCostByAreaResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateMaterialsCostByPlotIdQueryHandler> _logger;

    public CalculateMaterialsCostByPlotIdQueryHandler(IUnitOfWork unitOfWork, ILogger<CalculateMaterialsCostByPlotIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CalculateMaterialsCostByAreaResponse>> Handle(CalculateMaterialsCostByPlotIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // --- 1. Retrieve the plot to get its area ---
            var plot = await _unitOfWork.Repository<Plot>().FindAsync(
                match: p => p.Id == request.PlotId
            );

            if (plot == null)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure($"Plot with ID {request.PlotId} not found.", "PlotNotFound");
            }

            if (plot.Area <= 0)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("Plot area is not valid or is zero.", "InvalidPlotArea");
            }

            var currentDate = DateTime.UtcNow;
            var priceWarnings = new List<string>();

            // --- 2. Collect all material IDs ---
            var allMaterialIds = request.Tasks
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            // --- 3. Load material details ---
            var materials = await _unitOfWork.Repository<Material>().ListAsync(
                filter: m => allMaterialIds.Contains(m.Id) && m.IsActive
            );

            if (!materials.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No active materials found.", "MaterialsNotFound");
            }

            var materialsDict = materials.ToDictionary(m => m.Id, m => m);

            // --- 4. Load current prices for materials ---
            var allPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => allMaterialIds.Contains(p.MaterialId) && p.ValidFrom <= currentDate
            );

            // Get the most recent valid price for each material
            var currentPrices = allPrices
                .Where(p => !p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date)
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p);

            // --- 5. Initialize tracking ---
            decimal totalCost = 0M;
            var materialCostItems = new List<MaterialCostItem>();
            var taskCostBreakdowns = new List<TaskCostBreakdown>();

            // Dictionary to aggregate all materials
            var materialAggregation = new Dictionary<Guid, MaterialCostItem>();

            // --- 6. Process Tasks with Materials ---
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

                        // Aggregate for overall summary
                        if (!materialAggregation.ContainsKey(materialInput.MaterialId))
                        {
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
                totalCost += taskTotalCost;
            }

            // --- 7. Calculate totals ---
            var totalCostPerHa = totalCost / plot.Area;

            // Convert aggregation to list
            materialCostItems = materialAggregation.Values.ToList();

            if (!materialCostItems.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No valid material cost calculations could be performed.", "NoValidCalculations");
            }

            var response = new CalculateMaterialsCostByAreaResponse
            {
                Area = plot.Area,
                TotalCostPerHa = totalCostPerHa,
                TotalCostForArea = totalCost,
                MaterialCostItems = materialCostItems,
                TaskCostBreakdowns = taskCostBreakdowns,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any()
                ? $"Successfully calculated material costs for Plot ID {request.PlotId} with {priceWarnings.Count} warning(s)."
                : $"Successfully calculated material costs for Plot ID {request.PlotId}.";

            _logger.LogInformation("Calculated material costs for Plot ID {PlotId} (Area: {Area}ha). Total cost: {TotalCost}. Materials: {Count}",
                request.PlotId, plot.Area, totalCost, materialCostItems.Count);

            return Result<CalculateMaterialsCostByAreaResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating material costs for Plot ID {PlotId}", request.PlotId);
            return Result<CalculateMaterialsCostByAreaResponse>.Failure("An error occurred during cost calculation.", "CostCalculationFailed");
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

        // Calculate total quantity needed for the plot area
        var totalQuantityNeeded = quantityPerHa * area;

        // Calculate packages needed (ceiling)
        var packagesNeeded = material.IsPartition
            ? totalQuantityNeeded / amountPerMaterial
            : Math.Ceiling(totalQuantityNeeded / amountPerMaterial);

        // Calculate actual quantity after ceiling
        var actualQuantity = packagesNeeded * amountPerMaterial;

        // Calculate total cost
        var totalCost = packagesNeeded * priceInfo.PricePerMaterial;

        // Calculate cost per hectare
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