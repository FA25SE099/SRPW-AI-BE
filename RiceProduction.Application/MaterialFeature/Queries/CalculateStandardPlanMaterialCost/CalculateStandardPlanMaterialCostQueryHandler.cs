using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateStandardPlanMaterialCost;

public class CalculateStandardPlanMaterialCostQueryHandler : IRequestHandler<CalculateStandardPlanMaterialCostQuery, Result<CalculateMaterialsCostByAreaResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CalculateStandardPlanMaterialCostQueryHandler> _logger;

    public CalculateStandardPlanMaterialCostQueryHandler(IApplicationDbContext context, ILogger<CalculateStandardPlanMaterialCostQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<CalculateMaterialsCostByAreaResponse>> Handle(CalculateStandardPlanMaterialCostQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var (effectiveArea, areaError) = await ResolveEffectiveAreaAsync(request, cancellationToken);
            if (areaError != null)
            {
                return areaError;
            }

            var standardPlan = await _context.StandardPlans
                .AsNoTracking()
                .Include(sp => sp.StandardPlanStages.OrderBy(stage => stage.SequenceOrder))
                    .ThenInclude(stage => stage.StandardPlanTasks.OrderBy(task => task.SequenceOrder))
                        .ThenInclude(task => task.StandardPlanTaskMaterials)
                .AsSplitQuery()
                .FirstOrDefaultAsync(sp => sp.Id == request.StandardPlanId, cancellationToken);

            if (standardPlan == null)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure($"Standard Plan with ID {request.StandardPlanId} not found.", "StandardPlanNotFound");
            }

            var tasksFromPlan = standardPlan.StandardPlanStages
                .OrderBy(stage => stage.SequenceOrder)
                .SelectMany(stage => stage.StandardPlanTasks.OrderBy(task => task.SequenceOrder))
                .Where(task => task.StandardPlanTaskMaterials.Any())
                .ToList();

            if (!tasksFromPlan.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No tasks with materials found in the standard plan.", "NoMaterialsFound");
            }

            var currentDate = DateTime.UtcNow;
            var priceWarnings = new List<string>();

            var materialIds = tasksFromPlan
                .SelectMany(task => task.StandardPlanTaskMaterials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var materials = await _context.Materials
                .AsNoTracking()
                .Where(m => materialIds.Contains(m.Id) && m.IsActive)
                .ToListAsync(cancellationToken);

            if (!materials.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No active materials found.", "MaterialsNotFound");
            }

            var materialsDict = materials.ToDictionary(m => m.Id, m => m);
            var currentPrices = await MaterialCostHelper.GetCurrentPricesAsync(_context, materialIds, currentDate, cancellationToken);

            decimal totalCost = 0M;
            var taskCostBreakdowns = new List<TaskCostBreakdown>();
            var materialAggregation = new Dictionary<Guid, MaterialCostItem>();

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
                    var materialCost = MaterialCostHelper.CalculateMaterialCost(
                        materialInput.MaterialId,
                        materialInput.QuantityPerHa,
                        effectiveArea,
                        materialsDict,
                        currentPrices,
                        priceWarnings);

                    if (materialCost == null)
                    {
                        continue;
                    }

                    taskBreakdown.Materials.Add(materialCost);
                    taskTotalCost += materialCost.TotalCost;
                    MaterialCostHelper.AggregateMaterialCost(materialAggregation, materialCost, effectiveArea);
                }

                taskBreakdown.TotalTaskCost = taskTotalCost;
                taskCostBreakdowns.Add(taskBreakdown);
                totalCost += taskTotalCost;
            }

            var materialCostItems = materialAggregation.Values.ToList();

            if (!materialCostItems.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No valid material cost calculations could be performed.", "NoValidCalculations");
            }

            var response = new CalculateMaterialsCostByAreaResponse
            {
                Area = effectiveArea,
                TotalCostPerHa = totalCost / effectiveArea,
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

    private async Task<(decimal Area, Result<CalculateMaterialsCostByAreaResponse>? Error)> ResolveEffectiveAreaAsync(
        CalculateStandardPlanMaterialCostQuery request,
        CancellationToken cancellationToken)
    {
        if (request.PlotId.HasValue)
        {
            var plot = await _context.Plots
                .AsNoTracking()
                .Where(p => p.Id == request.PlotId.Value)
                .Select(p => new { p.Area })
                .FirstOrDefaultAsync(cancellationToken);
            if (plot == null)
            {
                return (0M, Result<CalculateMaterialsCostByAreaResponse>.Failure($"Plot with ID {request.PlotId.Value} not found.", "PlotNotFound"));
            }

            _logger.LogInformation("Using plot area: {Area} ha for plot {PlotId}", plot.Area, request.PlotId.Value);
            return (plot.Area, null);
        }

        if (request.Area.HasValue)
        {
            _logger.LogInformation("Using provided area: {Area} ha", request.Area.Value);
            return (request.Area.Value, null);
        }

        return (0M, Result<CalculateMaterialsCostByAreaResponse>.Failure("Either PlotId or Area must be provided.", "InvalidInput"));
    }
}
