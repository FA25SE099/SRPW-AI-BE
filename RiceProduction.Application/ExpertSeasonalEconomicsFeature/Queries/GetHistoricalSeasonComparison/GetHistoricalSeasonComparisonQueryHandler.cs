using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics;
using RiceProduction.Domain.Entities;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetHistoricalSeasonComparison
{
    public class GetHistoricalSeasonComparisonQueryHandler :
        IRequestHandler<GetHistoricalSeasonComparisonQuery, Result<HistoricalSeasonComparisonResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetHistoricalSeasonComparisonQueryHandler> _logger;

        public GetHistoricalSeasonComparisonQueryHandler(
            IApplicationDbContext context,
            ILogger<GetHistoricalSeasonComparisonQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<HistoricalSeasonComparisonResponse>> Handle(
            GetHistoricalSeasonComparisonQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var seasons = await GetSeasonsAsync(request, cancellationToken);
                if (!seasons.Any())
                {
                    return Result<HistoricalSeasonComparisonResponse>.Failure("No seasons found for comparison");
                }

                var seasonComparisonData = new List<SeasonComparisonData>(seasons.Count);
                foreach (var season in seasons)
                {
                    seasonComparisonData.Add(
                        await BuildSeasonComparisonDataAsync(season, request.ClusterId, cancellationToken));
                }

                var response = new HistoricalSeasonComparisonResponse
                {
                    Seasons = seasonComparisonData.OrderByDescending(s => s.Year).ToList(),
                    Trends = CalculateTrends(seasonComparisonData)
                };

                _logger.LogInformation(
                    "Retrieved historical comparison for {Count} seasons",
                    seasonComparisonData.Count);

                return Result<HistoricalSeasonComparisonResponse>.Success(
                    response,
                    "Successfully retrieved historical season comparison");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving historical season comparison");
                return Result<HistoricalSeasonComparisonResponse>.Failure(
                    "An error occurred while retrieving historical season comparison");
            }
        }

        private async Task<List<Season>> GetSeasonsAsync(
            GetHistoricalSeasonComparisonQuery request,
            CancellationToken cancellationToken)
        {
            IQueryable<Season> query = _context.Seasons.AsNoTracking();

            if (request.SeasonIds != null && request.SeasonIds.Any())
            {
                return await query
                    .Where(s => request.SeasonIds.Contains(s.Id))
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync(cancellationToken);
            }

            query = query.OrderByDescending(s => s.CreatedAt);

            if (request.Limit is > 0)
            {
                query = query.Take(request.Limit.Value);
            }

            return await query.ToListAsync(cancellationToken);
        }

        private async Task<SeasonComparisonData> BuildSeasonComparisonDataAsync(
            Season season,
            Guid? clusterId,
            CancellationToken cancellationToken)
        {
            var plotCultivations = await _context.PlotCultivations
                .AsNoTracking()
                .AsSplitQuery()
                .Include(pc => pc.Plot)
                    .ThenInclude(p => p.GroupPlots)
                        .ThenInclude(gp => gp.Group)
                .Include(pc => pc.RiceVariety)
                .Include(pc => pc.CultivationTasks)
                .Where(pc => pc.SeasonId == season.Id &&
                             (clusterId == null || pc.Plot.GroupPlots.Any(gp => gp.Group.ClusterId == clusterId)))
                .ToListAsync(cancellationToken);

            var riceVarietySeasons = await _context.RiceVarietySeasons
                .AsNoTracking()
                .Where(rvs => rvs.SeasonId == season.Id)
                .ToListAsync(cancellationToken);

            var totalArea = plotCultivations.Sum(pc => pc.Area ?? pc.Plot.Area);
            var uniqueFarmers = plotCultivations.Select(pc => pc.Plot.FarmerId).Distinct().Count();

            var allTasks = plotCultivations.SelectMany(pc => pc.CultivationTasks).ToList();
            var totalMaterialCost = allTasks.Sum(ct => ct.ActualMaterialCost);
            var totalServiceCost = allTasks.Sum(ct => ct.ActualServiceCost);
            var totalCost = totalMaterialCost + totalServiceCost;

            var totalExpectedYield = plotCultivations.Sum(pc =>
            {
                var varietySeason = riceVarietySeasons.FirstOrDefault(rvs => rvs.RiceVarietyId == pc.RiceVarietyId);
                var expectedYieldPerHa = varietySeason?.ExpectedYieldPerHectare ?? 0;
                var area = pc.Area ?? pc.Plot.Area;
                return expectedYieldPerHa * area;
            });

            var totalActualYield = plotCultivations.Sum(pc => pc.ActualYield ?? 0);
            var yieldVariancePercentage = totalExpectedYield > 0
                ? ((totalActualYield - totalExpectedYield) / totalExpectedYield) * 100
                : 0;

            var completedTasks = allTasks.Count(ct => ct.Status == TaskStatus.Completed);
            var totalTasks = allTasks.Count;
            var taskCompletionRate = totalTasks > 0 ? (completedTasks / (decimal)totalTasks) * 100 : 0;

            var efficiencyScore = CalculateEfficiencyScore(
                yieldVariancePercentage,
                taskCompletionRate,
                totalCost,
                totalActualYield);

            var yearMatch = Regex.Match(season.SeasonName, @"\d{4}");
            var year = yearMatch.Success ? int.Parse(yearMatch.Value) : DateTime.UtcNow.Year;

            return new SeasonComparisonData
            {
                SeasonId = season.Id,
                SeasonName = season.SeasonName,
                Year = year,
                IsActive = season.IsActive,
                TotalCultivations = plotCultivations.Count,
                TotalArea = totalArea,
                TotalFarmers = uniqueFarmers,
                TotalCost = totalCost,
                CostPerHectare = totalArea > 0 ? totalCost / totalArea : 0,
                MaterialCost = totalMaterialCost,
                ServiceCost = totalServiceCost,
                TotalExpectedYield = totalExpectedYield,
                TotalActualYield = totalActualYield,
                YieldPerHectare = totalArea > 0 ? totalActualYield / totalArea : 0,
                YieldVariancePercentage = yieldVariancePercentage,
                CompletedTasks = completedTasks,
                TotalTasks = totalTasks,
                TaskCompletionRate = taskCompletionRate,
                EfficiencyScore = efficiencyScore
            };
        }

        private static decimal CalculateEfficiencyScore(
            decimal yieldVariance,
            decimal taskCompletion,
            decimal totalCost,
            decimal totalYield)
        {
            var yieldScore = Math.Clamp(100 + yieldVariance, 0, 100);
            var costEfficiencyScore = totalYield > 0 && totalCost > 0
                ? Math.Min((totalYield / (totalCost / 1000000)) * 10, 100)
                : 0;

            return yieldScore * 0.4m + taskCompletion * 0.3m + costEfficiencyScore * 0.3m;
        }

        private static ComparisonTrends CalculateTrends(List<SeasonComparisonData> seasons)
        {
            var orderedSeasons = seasons.OrderBy(s => s.Year).ToList();

            return new ComparisonTrends
            {
                AreaTrend = CalculateTrendAnalysis("Total Area", orderedSeasons.Select(s => s.TotalArea).ToList()),
                CostTrend = CalculateTrendAnalysis("Cost per Hectare", orderedSeasons.Select(s => s.CostPerHectare).ToList()),
                YieldTrend = CalculateTrendAnalysis("Yield per Hectare", orderedSeasons.Select(s => s.YieldPerHectare).ToList()),
                EfficiencyTrend = CalculateTrendAnalysis("Efficiency Score", orderedSeasons.Select(s => s.EfficiencyScore).ToList()),
                BestSeason = seasons.OrderByDescending(s => s.EfficiencyScore).FirstOrDefault() ?? new SeasonComparisonData(),
                BestSeasonCriteria = "Highest Efficiency Score"
            };
        }

        private static TrendAnalysis CalculateTrendAnalysis(string metric, List<decimal> values)
        {
            if (!values.Any())
            {
                return new TrendAnalysis { Metric = metric, Direction = "No Data" };
            }

            string direction = "Stable";
            decimal changePercentage = 0;

            if (values.Count >= 2)
            {
                var first = values.First();
                var last = values.Last();

                if (first != 0)
                {
                    changePercentage = ((last - first) / first) * 100;
                }

                direction = changePercentage switch
                {
                    > 5 => "Increasing",
                    < -5 => "Decreasing",
                    _ => "Stable"
                };
            }

            return new TrendAnalysis
            {
                Metric = metric,
                Direction = direction,
                ChangePercentage = changePercentage,
                AverageValue = values.Average(),
                HighestValue = values.Max(),
                LowestValue = values.Min()
            };
        }
    }
}

