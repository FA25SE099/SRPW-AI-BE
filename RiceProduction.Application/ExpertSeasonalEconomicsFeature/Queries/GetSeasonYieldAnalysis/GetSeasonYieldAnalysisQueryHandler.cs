using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetSeasonYieldAnalysis
{
    public class GetSeasonYieldAnalysisQueryHandler :
        IRequestHandler<GetSeasonYieldAnalysisQuery, Result<SeasonYieldAnalysisResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetSeasonYieldAnalysisQueryHandler> _logger;

        public GetSeasonYieldAnalysisQueryHandler(
            IUnitOfWork unitOfWork,
            ILogger<GetSeasonYieldAnalysisQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<SeasonYieldAnalysisResponse>> Handle(
            GetSeasonYieldAnalysisQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var season = await _unitOfWork.Repository<Season>().FindAsync(s => s.Id == request.SeasonId);
                if (season == null)
                {
                    return Result<SeasonYieldAnalysisResponse>.Failure("Season not found");
                }

                var plotCultivations = await _unitOfWork.Repository<PlotCultivation>().ListAsync(
                    filter: pc => pc.SeasonId == request.SeasonId &&
                                  (request.GroupId == null || pc.Plot.GroupId == request.GroupId) &&
                                  (request.ClusterId == null || pc.Plot.Group!.ClusterId == request.ClusterId),
                    includeProperties: q => q
                        .Include(pc => pc.Plot)
                            .ThenInclude(p => p.Group)
                        .Include(pc => pc.RiceVariety)
                        .Include(pc => pc.CultivationTasks));

                var riceVarietySeasons = await _unitOfWork.Repository<RiceVarietySeason>().ListAsync(
                    filter: rvs => rvs.SeasonId == request.SeasonId);

                var totalCultivations = plotCultivations.Count;
                var harvestedCultivations = plotCultivations.Where(pc => pc.Status == CultivationStatus.Completed).ToList();
                var totalArea = plotCultivations.Sum(pc => pc.Area ?? pc.Plot.Area);
                var harvestedArea = harvestedCultivations.Sum(pc => pc.Area ?? pc.Plot.Area);

                var totalExpectedYield = plotCultivations.Sum(pc =>
                {
                    var varietySeason = riceVarietySeasons.FirstOrDefault(rvs => rvs.RiceVarietyId == pc.RiceVarietyId);
                    var expectedYieldPerHa = varietySeason?.ExpectedYieldPerHectare ?? 0;
                    var area = pc.Area ?? pc.Plot.Area;
                    return expectedYieldPerHa * area;
                });

                var totalActualYield = harvestedCultivations.Sum(pc => pc.ActualYield ?? 0);
                var yieldVariance = totalActualYield - totalExpectedYield;
                
                var yieldPerHaValues = harvestedCultivations
                    .Where(pc => pc.ActualYield.HasValue && (pc.Area ?? pc.Plot.Area) > 0)
                    .Select(pc => pc.ActualYield!.Value / (pc.Area ?? pc.Plot.Area))
                    .ToList();

                var avgYieldPerHa = yieldPerHaValues.Any() ? yieldPerHaValues.Average() : 0;
                var highestYieldPerHa = yieldPerHaValues.Any() ? yieldPerHaValues.Max() : 0;
                var lowestYieldPerHa = yieldPerHaValues.Any() ? yieldPerHaValues.Min() : 0;
                var expectedYieldPerHa = totalArea > 0 ? totalExpectedYield / totalArea : 0;

                var yieldByVariety = plotCultivations
                    .GroupBy(pc => new { pc.RiceVarietyId, pc.RiceVariety.VarietyName })
                    .Select(g =>
                    {
                        var varietySeason = riceVarietySeasons.FirstOrDefault(rvs => rvs.RiceVarietyId == g.Key.RiceVarietyId);
                        var expectedYieldPerHa = varietySeason?.ExpectedYieldPerHectare ?? 0;
                        var totalArea = g.Sum(pc => pc.Area ?? pc.Plot.Area);
                        var harvestedInGroup = g.Where(pc => pc.Status == CultivationStatus.Completed).ToList();
                        var totalActualYield = harvestedInGroup.Sum(pc => pc.ActualYield ?? 0);
                        var totalExpectedYield = expectedYieldPerHa * totalArea;
                        var actualYieldPerHa = totalArea > 0 ? totalActualYield / totalArea : 0;
                        var totalCost = g.SelectMany(pc => pc.CultivationTasks).Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost);

                        return new RiceVarietyYieldDetail
                        {
                            RiceVarietyId = g.Key.RiceVarietyId,
                            VarietyName = g.Key.VarietyName,
                            CultivationCount = g.Count(),
                            HarvestedCount = harvestedInGroup.Count,
                            TotalArea = totalArea,
                            ExpectedYieldPerHa = expectedYieldPerHa,
                            TotalExpectedYield = totalExpectedYield,
                            TotalActualYield = totalActualYield,
                            ActualYieldPerHa = actualYieldPerHa,
                            YieldVariance = totalActualYield - totalExpectedYield,
                            VariancePercentage = totalExpectedYield > 0 ? ((totalActualYield - totalExpectedYield) / totalExpectedYield) * 100 : 0,
                            TotalCost = totalCost,
                            CostPerTon = totalActualYield > 0 ? totalCost / totalActualYield : 0
                        };
                    })
                    .OrderByDescending(v => v.TotalActualYield)
                    .ToList();

                var yieldByGroup = plotCultivations
                    .Where(pc => pc.Plot.GroupId.HasValue)
                    .GroupBy(pc => new { pc.Plot.GroupId, GroupName = pc.Plot.Group!.Id.ToString() })
                    .Select(g =>
                    {
                        var totalArea = g.Sum(pc => pc.Area ?? pc.Plot.Area);
                        var harvestedInGroup = g.Where(pc => pc.Status == CultivationStatus.Completed).ToList();
                        var totalActualYield = harvestedInGroup.Sum(pc => pc.ActualYield ?? 0);
                        var totalExpectedYield = g.Sum(pc =>
                        {
                            var varietySeason = riceVarietySeasons.FirstOrDefault(rvs => rvs.RiceVarietyId == pc.RiceVarietyId);
                            var expectedYieldPerHa = varietySeason?.ExpectedYieldPerHectare ?? 0;
                            var area = pc.Area ?? pc.Plot.Area;
                            return expectedYieldPerHa * area;
                        });

                        return new GroupYieldDetail
                        {
                            GroupId = g.Key.GroupId!.Value,
                            GroupName = g.Key.GroupName,
                            PlotCount = g.Count(),
                            TotalArea = totalArea,
                            TotalExpectedYield = totalExpectedYield,
                            TotalActualYield = totalActualYield,
                            AverageYieldPerHa = totalArea > 0 ? totalActualYield / totalArea : 0,
                            YieldVariance = totalActualYield - totalExpectedYield,
                            HarvestedCount = harvestedInGroup.Count
                        };
                    })
                    .OrderByDescending(g => g.TotalActualYield)
                    .ToList();

                var performanceCategories = new List<YieldPerformanceCategory>
                {
                    new YieldPerformanceCategory { Category = "Excellent (>7 tons/ha)", MinYieldPerHa = 7, MaxYieldPerHa = decimal.MaxValue },
                    new YieldPerformanceCategory { Category = "Good (5-7 tons/ha)", MinYieldPerHa = 5, MaxYieldPerHa = 7 },
                    new YieldPerformanceCategory { Category = "Average (3-5 tons/ha)", MinYieldPerHa = 3, MaxYieldPerHa = 5 },
                    new YieldPerformanceCategory { Category = "Below Average (<3 tons/ha)", MinYieldPerHa = 0, MaxYieldPerHa = 3 }
                };

                foreach (var category in performanceCategories)
                {
                    var cultivationsInCategory = harvestedCultivations
                        .Where(pc =>
                        {
                            var area = pc.Area ?? pc.Plot.Area;
                            if (area <= 0 || !pc.ActualYield.HasValue) return false;
                            var yieldPerHa = pc.ActualYield.Value / area;
                            return yieldPerHa >= category.MinYieldPerHa && yieldPerHa < category.MaxYieldPerHa;
                        })
                        .ToList();

                    category.CultivationCount = cultivationsInCategory.Count;
                    category.TotalArea = cultivationsInCategory.Sum(pc => pc.Area ?? pc.Plot.Area);
                    category.Percentage = harvestedCultivations.Count > 0 ? (cultivationsInCategory.Count / (decimal)harvestedCultivations.Count) * 100 : 0;
                }

                var response = new SeasonYieldAnalysisResponse
                {
                    SeasonId = season.Id,
                    SeasonName = season.SeasonName,

                    Overview = new YieldOverview
                    {
                        TotalCultivations = totalCultivations,
                        HarvestedCultivations = harvestedCultivations.Count,
                        PendingHarvest = totalCultivations - harvestedCultivations.Count,
                        TotalArea = totalArea,
                        HarvestedArea = harvestedArea,
                        TotalExpectedYield = totalExpectedYield,
                        TotalActualYield = totalActualYield,
                        YieldVariance = yieldVariance,
                        VariancePercentage = totalExpectedYield > 0 ? (yieldVariance / totalExpectedYield) * 100 : 0,
                        AverageYieldPerHectare = avgYieldPerHa,
                        ExpectedYieldPerHectare = expectedYieldPerHa,
                        HighestYieldPerHectare = highestYieldPerHa,
                        LowestYieldPerHectare = lowestYieldPerHa
                    },

                    YieldByVariety = yieldByVariety,
                    YieldByGroup = yieldByGroup,
                    PerformanceCategories = performanceCategories
                };

                _logger.LogInformation(
                    "Retrieved yield analysis for season {SeasonId}: {HarvestedCount}/{TotalCount} harvested",
                    request.SeasonId, harvestedCultivations.Count, totalCultivations);

                return Result<SeasonYieldAnalysisResponse>.Success(
                    response,
                    "Successfully retrieved season yield analysis");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving yield analysis for season {SeasonId}", request.SeasonId);
                return Result<SeasonYieldAnalysisResponse>.Failure(
                    "An error occurred while retrieving season yield analysis");
            }
        }
    }
}

