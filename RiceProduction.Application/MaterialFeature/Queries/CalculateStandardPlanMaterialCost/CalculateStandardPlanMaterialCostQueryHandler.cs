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

            // Get Standard Plan with materials
            var standardPlan = await _unitOfWork.Repository<StandardPlan>()
                .GetQueryable()
                .Include(sp => sp.StandardPlanStages)
                    .ThenInclude(stage => stage.StandardPlanTasks)
                        .ThenInclude(task => task.StandardPlanTaskMaterials)
                .FirstOrDefaultAsync(sp => sp.Id == request.StandardPlanId, cancellationToken);

            if (standardPlan == null)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure($"Standard Plan with ID {request.StandardPlanId} not found.", "StandardPlanNotFound");
            }

            // Extract all materials from the standard plan and group by MaterialId
            var materialsFromPlan = standardPlan.StandardPlanStages
                .SelectMany(stage => stage.StandardPlanTasks)
                .SelectMany(task => task.StandardPlanTaskMaterials)
                .GroupBy(m => m.MaterialId)
                .Select(g => new
                {
                    MaterialId = g.Key,
                    TotalQuantityPerHa = g.Sum(m => m.QuantityPerHa)
                })
                .ToList();

            if (!materialsFromPlan.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No materials found in the standard plan.", "NoMaterialsFound");
            }

            var currentDate = DateTime.UtcNow;
            var priceWarnings = new List<string>();

            // Get unique material IDs
            var materialIds = materialsFromPlan.Select(m => m.MaterialId).ToList();

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

            // Calculate costs for each material
            var materialCostItems = new List<MaterialCostItem>();
            decimal totalCostForArea = 0M;

            foreach (var materialFromPlan in materialsFromPlan)
            {
                if (!materialsDict.TryGetValue(materialFromPlan.MaterialId, out var material))
                {
                    priceWarnings.Add($"Material ID {materialFromPlan.MaterialId} not found or is inactive.");
                    continue;
                }

                if (!currentPrices.TryGetValue(materialFromPlan.MaterialId, out var priceInfo))
                {
                    priceWarnings.Add($"No valid price found for material '{material.Name}' (ID: {material.Id}).");
                    continue;
                }

                var amountPerMaterial = material.AmmountPerMaterial.GetValueOrDefault(1M);
                if (amountPerMaterial <= 0) amountPerMaterial = 1M;

                // Calculate total quantity needed for the area
                var totalQuantityNeeded = materialFromPlan.TotalQuantityPerHa * effectiveArea;

                // Calculate packages needed (ceiling)
                var packagesNeeded = material.IsPartition
                    ? totalQuantityNeeded / amountPerMaterial
                    : Math.Ceiling(totalQuantityNeeded / amountPerMaterial);

                // Calculate actual quantity after ceiling
                var actualQuantity = packagesNeeded * amountPerMaterial;

                // Calculate total cost
                var totalCost = packagesNeeded * priceInfo.PricePerMaterial;

                // Calculate cost per hectare
                var costPerHa = totalCost / effectiveArea;

                materialCostItems.Add(new MaterialCostItem
                {
                    MaterialId = material.Id,
                    MaterialName = material.Name,
                    Unit = material.Unit,
                    QuantityPerHa = materialFromPlan.TotalQuantityPerHa,
                    TotalQuantityNeeded = totalQuantityNeeded,
                    AmountPerMaterial = amountPerMaterial,
                    PackagesNeeded = packagesNeeded,
                    ActualQuantity = actualQuantity,
                    PricePerMaterial = priceInfo.PricePerMaterial,
                    TotalCost = totalCost,
                    CostPerHa = costPerHa,
                    PriceValidFrom = priceInfo.ValidFrom
                });

                totalCostForArea += totalCost;
            }

            if (!materialCostItems.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No valid material cost calculations could be performed.", "NoValidCalculations");
            }

            // Calculate total cost per hectare
            var totalCostPerHa = totalCostForArea / effectiveArea;

            var response = new CalculateMaterialsCostByAreaResponse
            {
                Area = effectiveArea,
                TotalCostPerHa = totalCostPerHa,
                TotalCostForArea = totalCostForArea,
                MaterialCostItems = materialCostItems,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any()
                ? $"Successfully calculated material costs from standard plan with {priceWarnings.Count} warning(s)."
                : "Successfully calculated material costs from standard plan.";

            _logger.LogInformation("Calculated material costs for standard plan {PlanId}, area {Area}ha. Total cost: {TotalCost}. Materials: {Count}",
                request.StandardPlanId, effectiveArea, totalCostForArea, materialCostItems.Count);

            return Result<CalculateMaterialsCostByAreaResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating material costs from standard plan {PlanId}", request.StandardPlanId);
            return Result<CalculateMaterialsCostByAreaResponse>.Failure("An error occurred during cost calculation.", "CostCalculationFailed");
        }
    }
}
