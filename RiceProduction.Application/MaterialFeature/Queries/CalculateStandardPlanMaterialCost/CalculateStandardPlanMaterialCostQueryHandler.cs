using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateStandardPlanMaterialCost;

public class CalculateStandardPlanMaterialCostQueryHandler : IRequestHandler<CalculateStandardPlanMaterialCostQuery, Result<CalculateMaterialsCostByAreaResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateStandardPlanMaterialCostQueryHandler> _logger;

    public CalculateStandardPlanMaterialCostQueryHandler(IUnitOfWork unitOfWork, ILogger<CalculateStandardPlanMaterialCostQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CalculateMaterialsCostByAreaResponse>> Handle(CalculateStandardPlanMaterialCostQuery request, CancellationToken cancellationToken)
    {
        try
        {
            decimal effectiveArea;

            // Determine effective area
            if (request.PlotId.HasValue)
            {
                var plot = await _unitOfWork.Repository<Plot>().FindAsync(p => p.Id == request.PlotId.Value);
                if (plot == null)
                {
                    return Result<CalculateMaterialsCostByAreaResponse>.Failure($"Plot with ID {request.PlotId.Value} not found.", "PlotNotFound");
                }
                effectiveArea = plot.Area;
                _logger.LogInformation("Using plot area: {Area} ha for plot {PlotId}", effectiveArea, request.PlotId.Value);
            }
            else if (request.Area.HasValue)
            {
                effectiveArea = request.Area.Value;
                _logger.LogInformation("Using provided area: {Area} ha", effectiveArea);
            }
            else
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("Either PlotId or Area must be provided.", "InvalidInput");
            }

            // Get Standard Plan with materials - ORDERED BY STAGE AND TASK SEQUENCE
            var standardPlan = await _unitOfWork.Repository<StandardPlan>()
                .GetQueryable()
                .Include(sp => sp.StandardPlanStages.OrderBy(stage => stage.SequenceOrder)) // ? Order stages
                    .ThenInclude(stage => stage.StandardPlanTasks.OrderBy(task => task.SequenceOrder)) // ? Order tasks
                        .ThenInclude(task => task.StandardPlanTaskMaterials)
                .FirstOrDefaultAsync(sp => sp.Id == request.StandardPlanId, cancellationToken);

            if (standardPlan == null)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure($"Standard Plan with ID {request.StandardPlanId} not found.", "StandardPlanNotFound");
            }

            // Extract all tasks with materials from the standard plan - PRESERVE ORDERING
            var tasksFromPlan = standardPlan.StandardPlanStages
                .OrderBy(stage => stage.SequenceOrder) // ? Maintain stage order
                .SelectMany(stage => stage.StandardPlanTasks.OrderBy(task => task.SequenceOrder)) // ? Maintain task order within stage
                .Where(task => task.StandardPlanTaskMaterials.Any())
                .ToList();

            if (!tasksFromPlan.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No tasks with materials found in the standard plan.", "NoMaterialsFound");
            }

            var currentDate = DateTime.UtcNow;
            var priceWarnings = new List<string>();

            // Get unique material IDs from all tasks
            var materialIds = tasksFromPlan
                .SelectMany(task => task.StandardPlanTaskMaterials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            // Load material details
            var materials = await _unitOfWork.Repository<Material>().ListAsync(
                filter: m => materialIds.Contains(m.Id) && m.IsActive
            );

            if (!materials.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No active materials found.", "MaterialsNotFound");
            }

            var materialsDict = materials.ToDictionary(m => m.Id, m => m);

            // Load current prices for materials
            var allPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom <= currentDate
            );

            // Get the most recent valid price for each material
            var currentPrices = allPrices
                .Where(p => !p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date)
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p);

            // Initialize tracking
            decimal totalCost = 0M;
            var taskCostBreakdowns = new List<TaskCostBreakdown>();
            var materialAggregation = new Dictionary<Guid, MaterialCostItem>();

            // Process each task with its materials - IN ORDER
            foreach (var task in tasksFromPlan)
            {
                var taskBreakdown = new TaskCostBreakdown
                {
                    TaskName = task.TaskName,
                    TaskDescription = task.Description
                };

                decimal taskTotalCost = 0M;

                foreach (var materialInput in task.StandardPlanTaskMaterials)
                {
                    var materialCost = CalculateMaterialCost(
                        materialInput.MaterialId,
                        materialInput.QuantityPerHa,
                        effectiveArea,
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
                            existing.CostPerHa = existing.TotalCost / effectiveArea;
                        }
                    }
                }

                taskBreakdown.TotalTaskCost = taskTotalCost;
                taskCostBreakdowns.Add(taskBreakdown);
                totalCost += taskTotalCost;
            }

            // Convert aggregation to list
            var materialCostItems = materialAggregation.Values.ToList();

            if (!materialCostItems.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No valid material cost calculations could be performed.", "NoValidCalculations");
            }

            // Calculate total cost per hectare
            var totalCostPerHa = totalCost / effectiveArea;

            var response = new CalculateMaterialsCostByAreaResponse
            {
                Area = effectiveArea,
                TotalCostPerHa = totalCostPerHa,
                TotalCostForArea = totalCost,
                MaterialCostItems = materialCostItems,
                TaskCostBreakdowns = taskCostBreakdowns,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any()
                ? $"Successfully calculated material costs from standard plan with {priceWarnings.Count} warning(s)."
                : "Successfully calculated material costs from standard plan.";

            _logger.LogInformation("Calculated material costs for standard plan {PlanId}, area {Area}ha. Total cost: {TotalCost}. Tasks: {TaskCount} (ordered by sequence), Materials: {Count}",
                request.StandardPlanId, effectiveArea, totalCost, taskCostBreakdowns.Count, materialCostItems.Count);

            return Result<CalculateMaterialsCostByAreaResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating material costs from standard plan {PlanId}", request.StandardPlanId);
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
