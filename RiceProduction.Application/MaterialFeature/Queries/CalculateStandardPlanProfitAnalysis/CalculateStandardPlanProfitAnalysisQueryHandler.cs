using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateStandardPlanProfitAnalysis;

public class CalculateStandardPlanProfitAnalysisQueryHandler : IRequestHandler<CalculateStandardPlanProfitAnalysisQuery, Result<StandardPlanProfitAnalysisResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CalculateStandardPlanProfitAnalysisQueryHandler> _logger;

    public CalculateStandardPlanProfitAnalysisQueryHandler(IApplicationDbContext context, ILogger<CalculateStandardPlanProfitAnalysisQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<StandardPlanProfitAnalysisResponse>> Handle(CalculateStandardPlanProfitAnalysisQuery request, CancellationToken cancellationToken)
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
                .Include(sp => sp.StandardPlanStages)
                    .ThenInclude(stage => stage.StandardPlanTasks)
                        .ThenInclude(task => task.StandardPlanTaskMaterials)
                .AsSplitQuery()
                .FirstOrDefaultAsync(sp => sp.Id == request.StandardPlanId, cancellationToken);

            if (standardPlan == null)
            {
                return Result<StandardPlanProfitAnalysisResponse>.Failure($"Standard Plan with ID {request.StandardPlanId} not found.", "StandardPlanNotFound");
            }

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
            var materialIds = materialsFromPlan.Select(m => m.MaterialId).ToList();

            var materials = await _context.Materials
                .AsNoTracking()
                .Where(m => materialIds.Contains(m.Id) && m.IsActive)
                .ToListAsync(cancellationToken);

            if (!materials.Any())
            {
                return Result<StandardPlanProfitAnalysisResponse>.Failure("No active materials found.", "MaterialsNotFound");
            }

            var materialsDict = materials.ToDictionary(m => m.Id, m => m);
            var currentPrices = await MaterialCostHelper.GetCurrentPricesAsync(_context, materialIds, currentDate, cancellationToken);

            // Compute both 1ha baseline (for consistent per-hectare metrics) and actual-area material costs
            decimal materialCostPerHa = 0M;
            decimal totalMaterialCostForArea = 0M;
            var materialCostDetails = new List<MaterialCostSummary>();

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

                var amountPerMaterial = material.AmmountPerMaterial is > 0 ? material.AmmountPerMaterial.Value : 1M;
                var packagesForOneHa = MaterialCostHelper.CalculatePackagesNeeded(materialFromPlan.TotalQuantityPerHa, amountPerMaterial, material.IsPartition);
                materialCostPerHa += packagesForOneHa * priceInfo.PricePerMaterial;

                var totalQuantityForArea = materialFromPlan.TotalQuantityPerHa * effectiveArea;
                var packagesNeeded = MaterialCostHelper.CalculatePackagesNeeded(totalQuantityForArea, amountPerMaterial, material.IsPartition);
                var totalCost = packagesNeeded * priceInfo.PricePerMaterial;

                materialCostDetails.Add(new MaterialCostSummary
                {
                    MaterialId = material.Id,
                    MaterialName = material.Name,
                    Unit = material.Unit,
                    QuantityPerHa = materialFromPlan.TotalQuantityPerHa,
                    TotalQuantityForArea = totalQuantityForArea,
                    PackagesNeeded = packagesNeeded,
                    TotalCost = totalCost,
                    CostPerHa = totalCost / effectiveArea
                });

                totalMaterialCostForArea += totalCost;
            }

            if (!materialCostDetails.Any())
            {
                return Result<StandardPlanProfitAnalysisResponse>.Failure("No valid material cost calculations could be performed.", "NoValidCalculations");
            }

            var expectedRevenuePerHa = request.PricePerKgRice * request.ExpectedYieldPerHa;
            var totalCostPerHa = materialCostPerHa + request.OtherServiceCostPerHa;
            var profitPerHa = expectedRevenuePerHa - totalCostPerHa;
            var profitMarginPerHa = CalculateProfitMargin(profitPerHa, expectedRevenuePerHa);

            var expectedRevenueForArea = expectedRevenuePerHa * effectiveArea;
            var otherServiceCostForArea = request.OtherServiceCostPerHa * effectiveArea;
            var totalCostForArea = totalMaterialCostForArea + otherServiceCostForArea;
            var profitForArea = expectedRevenueForArea - totalCostForArea;
            var profitMarginForArea = CalculateProfitMargin(profitForArea, expectedRevenueForArea);

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

    private async Task<(decimal Area, Result<StandardPlanProfitAnalysisResponse>? Error)> ResolveEffectiveAreaAsync(
        CalculateStandardPlanProfitAnalysisQuery request,
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
                return (0M, Result<StandardPlanProfitAnalysisResponse>.Failure($"Plot with ID {request.PlotId.Value} not found.", "PlotNotFound"));
            }

            _logger.LogInformation("Using plot area: {Area} ha for plot {PlotId}", plot.Area, request.PlotId.Value);
            return (plot.Area, null);
        }

        if (request.Area.HasValue)
        {
            _logger.LogInformation("Using provided area: {Area} ha", request.Area.Value);
            return (request.Area.Value, null);
        }

        return (0M, Result<StandardPlanProfitAnalysisResponse>.Failure("Either PlotId or Area must be provided.", "InvalidInput"));
    }

    private static decimal CalculateProfitMargin(decimal profit, decimal revenue)
    {
        return revenue > 0 ? profit / revenue * 100 : 0;
    }
}
