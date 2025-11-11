using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetHistoricalSeasonComparison
{
    public class GetHistoricalSeasonComparisonQueryHandler :
        IRequestHandler<GetHistoricalSeasonComparisonQuery, Result<HistoricalSeasonComparisonResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetHistoricalSeasonComparisonQueryHandler> _logger;

        public GetHistoricalSeasonComparisonQueryHandler(
            IUnitOfWork unitOfWork,
            ILogger<GetHistoricalSeasonComparisonQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<HistoricalSeasonComparisonResponse>> Handle(
            GetHistoricalSeasonComparisonQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<Season> seasons;

                if (request.SeasonIds != null && request.SeasonIds.Any())
                {
                    seasons = await _unitOfWork.Repository<Season>().ListAsync(
                        filter: s => request.SeasonIds.Contains(s.Id),
                        orderBy: q => q.OrderByDescending(s => s.CreatedAt));
                }
                else
                {
                    seasons = await _unitOfWork.Repository<Season>().ListAsync(
                        orderBy: q => q.OrderByDescending(s => s.CreatedAt));
                    
                    if (request.Limit.HasValue && request.Limit.Value > 0)
                    {
                        seasons = seasons.Take(request.Limit.Value).ToList();
                    }
                }

                if (!seasons.Any())
                {
                    return Result<HistoricalSeasonComparisonResponse>.Failure("No seasons found for comparison");
                }

                var seasonComparisonData = new List<SeasonComparisonData>();

                foreach (var season in seasons)
                {
                    var plotCultivations = await _unitOfWork.Repository<PlotCultivation>().ListAsync(
                        filter: pc => pc.SeasonId == season.Id &&
                                      (request.ClusterId == null || pc.Plot.Group!.ClusterId == request.ClusterId),
                        includeProperties: q => q
                            .Include(pc => pc.Plot)
                                .ThenInclude(p => p.Group)
                            .Include(pc => pc.RiceVariety)
                            .Include(pc => pc.CultivationTasks));

                    var riceVarietySeasons = await _unitOfWork.Repository<RiceVarietySeason>().ListAsync(
                        filter: rvs => rvs.SeasonId == season.Id);

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

                    var yearMatch = System.Text.RegularExpressions.Regex.Match(season.SeasonName, @"\d{4}");
                    var year = yearMatch.Success ? int.Parse(yearMatch.Value) : DateTime.UtcNow.Year;

                    seasonComparisonData.Add(new SeasonComparisonData
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
                    });
                }

                var trends = CalculateTrends(seasonComparisonData);

                var response = new HistoricalSeasonComparisonResponse
                {
                    Seasons = seasonComparisonData.OrderByDescending(s => s.Year).ToList(),
                    Trends = trends
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

        private decimal CalculateEfficiencyScore(
            decimal yieldVariance,
            decimal taskCompletion,
            decimal totalCost,
            decimal totalYield)
        {
            var yieldScore = yieldVariance >= 0 ? Math.Min(yieldVariance + 100, 100) : Math.Max(100 + yieldVariance, 0);
            var completionScore = taskCompletion;
            var costEfficiencyScore = totalYield > 0 && totalCost > 0
                ? Math.Min((totalYield / (totalCost / 1000000)) * 10, 100)
                : 0;

            return (yieldScore * 0.4m + completionScore * 0.3m + costEfficiencyScore * 0.3m);
        }

        private ComparisonTrends CalculateTrends(List<SeasonComparisonData> seasons)
        {
            var orderedSeasons = seasons.OrderBy(s => s.Year).ToList();

            var areaTrend = CalculateTrendAnalysis("Total Area", orderedSeasons.Select(s => s.TotalArea).ToList());
            var costTrend = CalculateTrendAnalysis("Cost per Hectare", orderedSeasons.Select(s => s.CostPerHectare).ToList());
            var yieldTrend = CalculateTrendAnalysis("Yield per Hectare", orderedSeasons.Select(s => s.YieldPerHectare).ToList());
            var efficiencyTrend = CalculateTrendAnalysis("Efficiency Score", orderedSeasons.Select(s => s.EfficiencyScore).ToList());

            var bestSeason = seasons.OrderByDescending(s => s.EfficiencyScore).FirstOrDefault() ?? new SeasonComparisonData();

            return new ComparisonTrends
            {
                AreaTrend = areaTrend,
                CostTrend = costTrend,
                YieldTrend = yieldTrend,
                EfficiencyTrend = efficiencyTrend,
                BestSeason = bestSeason,
                BestSeasonCriteria = "Highest Efficiency Score"
            };
        }

        private TrendAnalysis CalculateTrendAnalysis(string metric, List<decimal> values)
        {
            if (!values.Any())
            {
                return new TrendAnalysis { Metric = metric, Direction = "No Data" };
            }

            var average = values.Average();
            var highest = values.Max();
            var lowest = values.Min();

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

                direction = changePercentage > 5 ? "Increasing" :
                           changePercentage < -5 ? "Decreasing" : "Stable";
            }

            return new TrendAnalysis
            {
                Metric = metric,
                Direction = direction,
                ChangePercentage = changePercentage,
                AverageValue = average,
                HighestValue = highest,
                LowestValue = lowest
            };
        }
    }
}

