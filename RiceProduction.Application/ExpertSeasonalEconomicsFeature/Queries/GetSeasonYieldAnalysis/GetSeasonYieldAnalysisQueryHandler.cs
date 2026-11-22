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
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetSeasonYieldAnalysisQueryHandler> _logger;

        public GetSeasonYieldAnalysisQueryHandler(
            IApplicationDbContext context,
            ILogger<GetSeasonYieldAnalysisQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<SeasonYieldAnalysisResponse>> Handle(
            GetSeasonYieldAnalysisQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var season = await _context.Seasons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == request.SeasonId, cancellationToken);
                if (season == null)
                {
                    return Result<SeasonYieldAnalysisResponse>.Failure("Season not found");
                }

                var plotIds = request.GroupId.HasValue
                    ? await _context.Plots
                        .AsNoTracking()
                        .Where(p => p.GroupPlots.Any(gp => gp.GroupId == request.GroupId.Value) &&
                                    p.Status == PlotStatus.Active)
                        .Select(p => p.Id)
                        .ToListAsync(cancellationToken)
                    : new List<Guid>();

                var plotCultivations = await _context.PlotCultivations
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(pc => pc.Plot)
                        .ThenInclude(p => p.GroupPlots)
                            .ThenInclude(gp => gp.Group)
                    .Include(pc => pc.RiceVariety)
                    .Include(pc => pc.CultivationTasks)
                    .Where(pc => pc.SeasonId == request.SeasonId &&
                                 (request.GroupId == null || plotIds.Contains(pc.PlotId)) &&
                                 (request.ClusterId == null || pc.Plot.GroupPlots.Any(gp => gp.Group.ClusterId == request.ClusterId)))
                    .ToListAsync(cancellationToken);

                var riceVarietySeasons = await _context.RiceVarietySeasons
                    .AsNoTracking()
                    .Where(rvs => rvs.SeasonId == request.SeasonId)
                    .ToListAsync(cancellationToken);

                var varietySeasonsByVarietyId = riceVarietySeasons
                    .GroupBy(rvs => rvs.RiceVarietyId)
                    .ToDictionary(g => g.Key, g => g.First());

                var totalCultivations = plotCultivations.Count;
                var harvestedCultivations = plotCultivations
                    .Where(pc => pc.Status == CultivationStatus.Completed)
                    .ToList();

                var totalArea = plotCultivations.Sum(GetArea);
                var harvestedArea = harvestedCultivations.Sum(GetArea);
                var totalExpectedYield = plotCultivations.Sum(pc => GetExpectedYield(pc, varietySeasonsByVarietyId));
                var totalActualYield = harvestedCultivations.Sum(pc => pc.ActualYield ?? 0);
                var yieldVariance = totalActualYield - totalExpectedYield;

                var yieldPerHaValues = harvestedCultivations
                    .Where(pc => pc.ActualYield.HasValue && GetArea(pc) > 0)
                    .Select(pc => pc.ActualYield!.Value / GetArea(pc))
                    .ToList();

                var hasYieldValues = yieldPerHaValues.Count > 0;
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
                        AverageYieldPerHectare = hasYieldValues ? yieldPerHaValues.Average() : 0,
                        ExpectedYieldPerHectare = totalArea > 0 ? totalExpectedYield / totalArea : 0,
                        HighestYieldPerHectare = hasYieldValues ? yieldPerHaValues.Max() : 0,
                        LowestYieldPerHectare = hasYieldValues ? yieldPerHaValues.Min() : 0
                    },
                    YieldByVariety = BuildYieldByVariety(plotCultivations, varietySeasonsByVarietyId),
                    YieldByGroup = BuildYieldByGroup(plotCultivations, varietySeasonsByVarietyId),
                    PerformanceCategories = BuildPerformanceCategories(harvestedCultivations)
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

        private static decimal GetArea(PlotCultivation pc) => pc.Area ?? pc.Plot.Area;

        private static decimal GetExpectedYieldPerHa(
            Dictionary<Guid, RiceVarietySeason> varietySeasonsByVarietyId,
            Guid riceVarietyId) =>
            varietySeasonsByVarietyId.GetValueOrDefault(riceVarietyId)?.ExpectedYieldPerHectare ?? 0;

        private static decimal GetExpectedYield(
            PlotCultivation pc,
            Dictionary<Guid, RiceVarietySeason> varietySeasonsByVarietyId) =>
            GetExpectedYieldPerHa(varietySeasonsByVarietyId, pc.RiceVarietyId) * GetArea(pc);

        private static List<RiceVarietyYieldDetail> BuildYieldByVariety(
            IReadOnlyList<PlotCultivation> plotCultivations,
            Dictionary<Guid, RiceVarietySeason> varietySeasonsByVarietyId) =>
            plotCultivations
                .GroupBy(pc => new { pc.RiceVarietyId, pc.RiceVariety.VarietyName })
                .Select(g =>
                {
                    var expectedYieldPerHa = GetExpectedYieldPerHa(varietySeasonsByVarietyId, g.Key.RiceVarietyId);
                    var totalArea = g.Sum(GetArea);
                    var harvestedInGroup = g.Where(pc => pc.Status == CultivationStatus.Completed).ToList();
                    var totalActualYield = harvestedInGroup.Sum(pc => pc.ActualYield ?? 0);
                    var totalExpectedYield = expectedYieldPerHa * totalArea;
                    var yieldVariance = totalActualYield - totalExpectedYield;
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
                        ActualYieldPerHa = totalArea > 0 ? totalActualYield / totalArea : 0,
                        YieldVariance = yieldVariance,
                        VariancePercentage = totalExpectedYield > 0 ? (yieldVariance / totalExpectedYield) * 100 : 0,
                        TotalCost = totalCost,
                        CostPerTon = totalActualYield > 0 ? totalCost / totalActualYield : 0
                    };
                })
                .OrderByDescending(v => v.TotalActualYield)
                .ToList();

        private static List<GroupYieldDetail> BuildYieldByGroup(
            IReadOnlyList<PlotCultivation> plotCultivations,
            Dictionary<Guid, RiceVarietySeason> varietySeasonsByVarietyId) =>
            plotCultivations
                .Where(pc => pc.Plot.GroupPlots.Any())
                .SelectMany(pc => pc.Plot.GroupPlots.Select(gp => new { PlotCultivation = pc, GroupPlot = gp }))
                .GroupBy(x => new { x.GroupPlot.GroupId, GroupName = x.GroupPlot.Group.Id.ToString() })
                .Select(g =>
                {
                    var totalArea = g.Sum(x => GetArea(x.PlotCultivation));
                    var harvestedInGroup = g.Where(x => x.PlotCultivation.Status == CultivationStatus.Completed).ToList();
                    var totalActualYield = harvestedInGroup.Sum(x => x.PlotCultivation.ActualYield ?? 0);
                    var totalExpectedYield = g.Sum(x => GetExpectedYield(x.PlotCultivation, varietySeasonsByVarietyId));

                    return new GroupYieldDetail
                    {
                        GroupId = g.Key.GroupId,
                        GroupName = g.Key.GroupName,
                        PlotCount = g.Select(x => x.PlotCultivation.PlotId).Distinct().Count(),
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

        private static List<YieldPerformanceCategory> BuildPerformanceCategories(
            List<PlotCultivation> harvestedCultivations)
        {
            var performanceCategories = new List<YieldPerformanceCategory>
            {
                new() { Category = "Excellent (>7 tons/ha)", MinYieldPerHa = 7, MaxYieldPerHa = decimal.MaxValue },
                new() { Category = "Good (5-7 tons/ha)", MinYieldPerHa = 5, MaxYieldPerHa = 7 },
                new() { Category = "Average (3-5 tons/ha)", MinYieldPerHa = 3, MaxYieldPerHa = 5 },
                new() { Category = "Below Average (<3 tons/ha)", MinYieldPerHa = 0, MaxYieldPerHa = 3 }
            };

            var harvestedWithYield = harvestedCultivations
                .Select(pc => new { Area = GetArea(pc), pc.ActualYield })
                .Where(x => x.Area > 0 && x.ActualYield.HasValue)
                .Select(x => new { x.Area, YieldPerHa = x.ActualYield!.Value / x.Area })
                .ToList();

            foreach (var category in performanceCategories)
            {
                var inCategory = harvestedWithYield
                    .Where(x => x.YieldPerHa >= category.MinYieldPerHa && x.YieldPerHa < category.MaxYieldPerHa)
                    .ToList();

                category.CultivationCount = inCategory.Count;
                category.TotalArea = inCategory.Sum(x => x.Area);
                category.Percentage = harvestedCultivations.Count > 0
                    ? (inCategory.Count / (decimal)harvestedCultivations.Count) * 100
                    : 0;
            }

            return performanceCategories;
        }
    }
}

