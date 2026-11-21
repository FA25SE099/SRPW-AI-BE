using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByArea;

public class CalculateMaterialsCostByAreaQueryHandler : IRequestHandler<CalculateMaterialsCostByAreaQuery, Result<CalculateMaterialsCostByAreaResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateMaterialsCostByAreaQueryHandler> _logger;

    public CalculateMaterialsCostByAreaQueryHandler(IUnitOfWork unitOfWork, ILogger<CalculateMaterialsCostByAreaQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CalculateMaterialsCostByAreaResponse>> Handle(CalculateMaterialsCostByAreaQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var currentDate = DateTime.UtcNow;
            var priceWarnings = new List<string>();

            var allMaterialIds = request.Tasks
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var materials = await _unitOfWork.Repository<Material>().ListAsync(
                filter: m => allMaterialIds.Contains(m.Id) && m.IsActive
            );

            if (!materials.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No active materials found.", "MaterialsNotFound");
            }

            var materialsDict = materials.ToDictionary(m => m.Id, m => m);
            var currentPrices = await MaterialCostHelper.GetCurrentPricesAsync(_unitOfWork, allMaterialIds, currentDate);

            decimal totalCost = 0M;
            var taskCostBreakdowns = new List<TaskCostBreakdown>();
            var materialAggregation = new Dictionary<Guid, MaterialCostItem>();

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
                    var materialCost = MaterialCostHelper.CalculateMaterialCost(
                        materialInput.MaterialId,
                        materialInput.QuantityPerHa,
                        request.Area,
                        materialsDict,
                        currentPrices,
                        priceWarnings);

                    if (materialCost == null) continue;

                    taskBreakdown.Materials.Add(materialCost);
                    taskTotalCost += materialCost.TotalCost;
                    MaterialCostHelper.AggregateMaterialCost(materialAggregation, materialCost, request.Area);
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
                Area = request.Area,
                TotalCostPerHa = totalCost / request.Area,
                TotalCostForArea = totalCost,
                MaterialCostItems = materialCostItems,
                TaskCostBreakdowns = taskCostBreakdowns,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any()
                ? $"Successfully calculated material costs with {priceWarnings.Count} warning(s)."
                : "Successfully calculated material costs.";

            _logger.LogInformation("Calculated material costs for area {Area}ha. Total cost: {TotalCost}. Materials: {Count}",
                request.Area, totalCost, materialCostItems.Count);

            return Result<CalculateMaterialsCostByAreaResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating material costs for area {Area}ha", request.Area);
            return Result<CalculateMaterialsCostByAreaResponse>.Failure("An error occurred during cost calculation.", "CostCalculationFailed");
        }
    }
}