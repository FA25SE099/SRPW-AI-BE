using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateStandardPlanProfitAnalysis;

public class CalculateStandardPlanProfitAnalysisQueryHandler : IRequestHandler<CalculateStandardPlanProfitAnalysisQuery, Result<StandardPlanProfitAnalysisResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateStandardPlanProfitAnalysisQueryHandler> _logger;

    public CalculateStandardPlanProfitAnalysisQueryHandler(IUnitOfWork unitOfWork, ILogger<CalculateStandardPlanProfitAnalysisQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<StandardPlanProfitAnalysisResponse>> Handle(CalculateStandardPlanProfitAnalysisQuery request, CancellationToken cancellationToken)
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
                    return Result<StandardPlanProfitAnalysisResponse>.Failure($"Plot with ID {request.PlotId.Value} not found.", "PlotNotFound");
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
                return Result<StandardPlanProfitAnalysisResponse>.Failure("Either PlotId or Area must be provided.", "InvalidInput");
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
                return Result<StandardPlanProfitAnalysisResponse>.Failure($"Standard Plan with ID {request.StandardPlanId} not found.", "StandardPlanNotFound");
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
                return Result<StandardPlanProfitAnalysisResponse>.Failure("No materials found in the standard plan.", "NoMaterialsFound");
            }

            var currentDate = DateTime.UtcNow;
            var warnings = new List<string>();

            // Get unique material IDs
            var materialIds = materialsFromPlan.Select(m => m.MaterialId).ToList();

            // Load material details
            var materials = await _unitOfWork.Repository<Material>().ListAsync(
                filter: m => materialIds.Contains(m.Id) && m.IsActive
            );

            if (!materials.Any())
            {
                return Result<StandardPlanProfitAnalysisResponse>.Failure("No active materials found.", "MaterialsNotFound");
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

            // FIRST: Calculate cost for exactly 1 hectare (for consistent per-hectare metrics)
            decimal materialCostForOneHa = 0M;

            foreach (var materialFromPlan in materialsFromPlan)
            {
                if (!materialsDict.TryGetValue(materialFromPlan.MaterialId, out var material))
                {
                    continue;
                }

                if (!currentPrices.TryGetValue(materialFromPlan.MaterialId, out var priceInfo))
                {
                    continue;
                }

                var amountPerMaterial = material.AmmountPerMaterial.GetValueOrDefault(1M);
                if (amountPerMaterial <= 0) amountPerMaterial = 1M;

                // Calculate for exactly 1 hectare
                var quantityForOneHa = materialFromPlan.TotalQuantityPerHa;
                var packagesForOneHa = material.IsPartition
                    ? quantityForOneHa / amountPerMaterial
                    : Math.Ceiling(quantityForOneHa / amountPerMaterial);
                var costForOneHa = packagesForOneHa * priceInfo.PricePerMaterial;

                materialCostForOneHa += costForOneHa;
            }

            // SECOND: Calculate material costs for the actual area provided
            var materialCostDetails = new List<MaterialCostSummary>();
            decimal totalMaterialCostForArea = 0M;

            foreach (var materialFromPlan in materialsFromPlan)
            {
                if (!materialsDict.TryGetValue(materialFromPlan.MaterialId, out var material))
                {
                    warnings.Add($"Material ID {materialFromPlan.MaterialId} not found or is inactive.");
                    continue;
                }

                if (!currentPrices.TryGetValue(materialFromPlan.MaterialId, out var priceInfo))
                {
                    warnings.Add($"No valid price found for material '{material.Name}' (ID: {material.Id}).");
                    continue;
                }

                var amountPerMaterial = material.AmmountPerMaterial.GetValueOrDefault(1M);
                if (amountPerMaterial <= 0) amountPerMaterial = 1M;

                // Calculate total quantity needed for the area
                var totalQuantityForArea = materialFromPlan.TotalQuantityPerHa * effectiveArea;

                // Calculate packages needed (ceiling)
                var packagesNeeded = material.IsPartition
                    ? totalQuantityForArea / amountPerMaterial
                    : Math.Ceiling(totalQuantityForArea / amountPerMaterial);

                // Calculate total cost
                var totalCost = packagesNeeded * priceInfo.PricePerMaterial;

                // Calculate cost per hectare from the actual total area calculation
                var costPerHa = totalCost / effectiveArea;

                materialCostDetails.Add(new MaterialCostSummary
                {
                    MaterialId = material.Id,
                    MaterialName = material.Name,
                    Unit = material.Unit,
                    QuantityPerHa = materialFromPlan.TotalQuantityPerHa,
                    TotalQuantityForArea = totalQuantityForArea,
                    PackagesNeeded = packagesNeeded,
                    TotalCost = totalCost,
                    CostPerHa = costPerHa
                });

                totalMaterialCostForArea += totalCost;
            }

            if (!materialCostDetails.Any())
            {
                return Result<StandardPlanProfitAnalysisResponse>.Failure("No valid material cost calculations could be performed.", "NoValidCalculations");
            }

            // Use the consistent 1ha cost for per-hectare metrics
            var materialCostPerHa = materialCostForOneHa;

            // Calculate revenue per hectare
            var expectedRevenuePerHa = request.PricePerKgRice * request.ExpectedYieldPerHa;

            // Calculate total cost per hectare (using the consistent 1ha material cost)
            var totalCostPerHa = materialCostPerHa + request.OtherServiceCostPerHa;

            // Calculate profit per hectare
            var profitPerHa = expectedRevenuePerHa - totalCostPerHa;

            // Calculate profit margin per hectare
            var profitMarginPerHa = expectedRevenuePerHa > 0 ? (profitPerHa / expectedRevenuePerHa * 100) : 0;

            // Calculate values for the given area
            var expectedRevenueForArea = expectedRevenuePerHa * effectiveArea;
            var otherServiceCostForArea = request.OtherServiceCostPerHa * effectiveArea;
            var totalCostForArea = totalMaterialCostForArea + otherServiceCostForArea;
            var profitForArea = expectedRevenueForArea - totalCostForArea;
            var profitMarginForArea = expectedRevenueForArea > 0 ? (profitForArea / expectedRevenueForArea * 100) : 0;

            var response = new StandardPlanProfitAnalysisResponse
            {
                Area = effectiveArea,
                PricePerKgRice = request.PricePerKgRice,
                ExpectedYieldPerHa = request.ExpectedYieldPerHa,
                ExpectedRevenuePerHa = expectedRevenuePerHa,
                MaterialCostPerHa = materialCostPerHa,
                OtherServiceCostPerHa = request.OtherServiceCostPerHa,
                TotalCostPerHa = totalCostPerHa,
                ProfitPerHa = profitPerHa,
                ProfitMarginPerHa = profitMarginPerHa,
                ExpectedRevenueForArea = expectedRevenueForArea,
                MaterialCostForArea = totalMaterialCostForArea,
                OtherServiceCostForArea = otherServiceCostForArea,
                TotalCostForArea = totalCostForArea,
                ProfitForArea = profitForArea,
                ProfitMarginForArea = profitMarginForArea,
                MaterialCostDetails = materialCostDetails,
                Warnings = warnings
            };

            var message = warnings.Any()
                ? $"Successfully calculated profit analysis with {warnings.Count} warning(s)."
                : "Successfully calculated profit analysis.";

            _logger.LogInformation(
                "Calculated profit analysis for standard plan {PlanId}, area {Area}ha. " +
                "Revenue per ha: {Revenue}, Cost per ha: {Cost}, Profit per ha: {Profit}, Profit margin: {Margin}%",
                request.StandardPlanId, effectiveArea, expectedRevenuePerHa, totalCostPerHa, profitPerHa, profitMarginPerHa);

            return Result<StandardPlanProfitAnalysisResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating profit analysis from standard plan {PlanId}", request.StandardPlanId);
            return Result<StandardPlanProfitAnalysisResponse>.Failure("An error occurred during profit analysis calculation.", "ProfitAnalysisCalculationFailed");
        }
    }
}
