using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetSeasonalEconomicOverview
{
    public class GetSeasonalEconomicOverviewQueryHandler :
        IRequestHandler<GetSeasonalEconomicOverviewQuery, Result<SeasonalEconomicOverviewResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetSeasonalEconomicOverviewQueryHandler> _logger;

        public GetSeasonalEconomicOverviewQueryHandler(
            IUnitOfWork unitOfWork,
            ILogger<GetSeasonalEconomicOverviewQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<SeasonalEconomicOverviewResponse>> Handle(
            GetSeasonalEconomicOverviewQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var season = await _unitOfWork.Repository<Season>().FindAsync(s => s.Id == request.SeasonId);
                if (season == null)
                {
                    return Result<SeasonalEconomicOverviewResponse>.Failure("Season not found");
                }

                var plotIds = request.GroupId.HasValue
                    ? (await _unitOfWork.PlotRepository.GetPlotsForGroupAsync(request.GroupId.Value, cancellationToken))
                        .Select(p => p.Id)
                        .ToList()
                    : new List<Guid>();

                var plotCultivations = await _unitOfWork.Repository<PlotCultivation>().ListAsync(
                    filter: pc => pc.SeasonId == request.SeasonId &&
                                  (request.GroupId == null || plotIds.Contains(pc.PlotId)) &&
                                  (request.ClusterId == null || pc.Plot.GroupPlots.Any(gp => gp.Group.ClusterId == request.ClusterId)),
                    includeProperties: q => q
                        .Include(pc => pc.Plot)
                            .ThenInclude(p => p.Farmer)
                        .Include(pc => pc.Plot)
                            .ThenInclude(p => p.GroupPlots)
                                .ThenInclude(gp => gp.Group)
                                    .ThenInclude(g => g.Cluster)
                        .Include(pc => pc.RiceVariety)
                        .Include(pc => pc.CultivationTasks)
                            .ThenInclude(ct => ct.CultivationTaskMaterials)
                                .ThenInclude(ctm => ctm.Material));

                var riceVarietySeasons = await _unitOfWork.Repository<RiceVarietySeason>().ListAsync(
                    filter: rvs => rvs.SeasonId == request.SeasonId);

                var uavInvoices = await _unitOfWork.Repository<UavInvoice>().ListAsync(
                    filter: inv => inv.UavServiceOrder != null &&
                                   inv.UavServiceOrder.Group!.YearSeason != null &&
                                   inv.UavServiceOrder.Group.YearSeason.SeasonId == request.SeasonId &&
                                   (request.GroupId == null || inv.UavServiceOrder.GroupId == request.GroupId) &&
                                   (request.ClusterId == null || inv.UavServiceOrder.Group.ClusterId == request.ClusterId),
                    includeProperties: q => q
                        .Include(inv => inv.UavServiceOrder!)
                            .ThenInclude(uso => uso.Group)
                                .ThenInclude(g => g.YearSeason));

                var totalArea = plotCultivations.Sum(GetArea);
                var uniqueFarmers = plotCultivations.Select(pc => pc.Plot.FarmerId).Distinct().Count();
                var uniqueGroups = plotCultivations
                    .SelectMany(pc => pc.Plot.GroupPlots)
                    .Select(gp => gp.GroupId)
                    .Distinct()
                    .Count();

                var allTasks = plotCultivations.SelectMany(pc => pc.CultivationTasks).ToList();
                var totalTasks = allTasks.Count;
                var completedTasks = allTasks.Count(ct => ct.Status == TaskStatus.Completed);

                var totalActualMaterialCost = allTasks.Sum(ct => ct.ActualMaterialCost);
                var totalActualServiceCost = allTasks.Sum(ct => ct.ActualServiceCost);
                var totalUavCost = uavInvoices
                    .Where(inv => inv.Status == InvoiceStatus.Paid)
                    .Sum(inv => inv.TotalAmount);
                var totalActualCost = totalActualMaterialCost + totalActualServiceCost + totalUavCost;

                var totalEstimatedCost = allTasks
                    .SelectMany(ct => ct.ProductionPlanTask.ProductionPlanTaskMaterials)
                    .Sum(pptm => pptm.EstimatedAmount ?? 0);

                var totalExpectedYield = CalculateTotalExpectedYield(plotCultivations, riceVarietySeasons);
                var totalActualYield = plotCultivations.Sum(pc => pc.ActualYield ?? 0);
                var harvestedCount = plotCultivations.Count(pc => pc.Status == CultivationStatus.Completed);
                var pendingHarvestCount = plotCultivations.Count(pc => pc.Status == CultivationStatus.InProgress);

                var response = new SeasonalEconomicOverviewResponse
                {
                    SeasonId = season.Id,
                    SeasonName = season.SeasonName,
                    StartDate = season.StartDate,
                    EndDate = season.EndDate,
                    IsActive = season.IsActive,
                    Cultivation = new CultivationSummary
                    {
                        TotalPlotCultivations = plotCultivations.Count,
                        TotalAreaCultivated = totalArea,
                        TotalGroups = uniqueGroups,
                        TotalFarmers = uniqueFarmers,
                        TotalTasks = totalTasks,
                        CompletedTasks = completedTasks,
                        TaskCompletionRate = totalTasks > 0 ? (completedTasks / (decimal)totalTasks) * 100 : 0
                    },
                    Costs = new CostSummary
                    {
                        TotalEstimatedCost = totalEstimatedCost,
                        TotalActualCost = totalActualCost,
                        CostVariance = totalActualCost - totalEstimatedCost,
                        ActualMaterialCost = totalActualMaterialCost,
                        ActualServiceCost = totalActualServiceCost,
                        CostPerHectare = totalArea > 0 ? totalActualCost / totalArea : 0,
                        UavServiceCost = totalUavCost
                    },
                    Yields = new YieldSummary
                    {
                        TotalExpectedYield = totalExpectedYield,
                        TotalActualYield = totalActualYield,
                        YieldVariance = totalActualYield - totalExpectedYield,
                        AverageYieldPerHectare = totalArea > 0 ? totalActualYield / totalArea : 0,
                        HarvestedCultivations = harvestedCount,
                        PendingHarvest = pendingHarvestCount
                    },
                    VarietyDistribution = BuildVarietyDistribution(plotCultivations, totalArea),
                    StatusBreakdown = BuildStatusBreakdown(plotCultivations)
                };

                _logger.LogInformation(
                    "Retrieved seasonal economic overview for season {SeasonId}: {CultivationCount} cultivations, {TotalArea} ha",
                    request.SeasonId, plotCultivations.Count, totalArea);

                return Result<SeasonalEconomicOverviewResponse>.Success(
                    response,
                    "Successfully retrieved seasonal economic overview");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving seasonal economic overview for season {SeasonId}", request.SeasonId);
                return Result<SeasonalEconomicOverviewResponse>.Failure(
                    "An error occurred while retrieving seasonal economic overview");
            }
        }

        private static decimal GetArea(PlotCultivation pc) => pc.Area ?? pc.Plot.Area;

        private static decimal CalculateTotalExpectedYield(
            IReadOnlyList<PlotCultivation> plotCultivations,
            IReadOnlyList<RiceVarietySeason> riceVarietySeasons)
        {
            var varietySeasonsByVarietyId = riceVarietySeasons
                .GroupBy(rvs => rvs.RiceVarietyId)
                .ToDictionary(g => g.Key, g => g.First());

            return plotCultivations.Sum(pc =>
            {
                var expectedYieldPerHa = varietySeasonsByVarietyId.GetValueOrDefault(pc.RiceVarietyId)?.ExpectedYieldPerHectare ?? 0;
                return expectedYieldPerHa * GetArea(pc);
            });
        }

        private static List<RiceVarietyDistribution> BuildVarietyDistribution(
            IReadOnlyList<PlotCultivation> plotCultivations,
            decimal totalArea) =>
            plotCultivations
                .GroupBy(pc => new { pc.RiceVarietyId, pc.RiceVariety.VarietyName })
                .Select(g =>
                {
                    var varietyArea = g.Sum(GetArea);

                    return new RiceVarietyDistribution
                    {
                        RiceVarietyId = g.Key.RiceVarietyId,
                        VarietyName = g.Key.VarietyName,
                        CultivationCount = g.Count(),
                        TotalArea = varietyArea,
                        Percentage = totalArea > 0 ? (varietyArea / totalArea) * 100 : 0
                    };
                })
                .OrderByDescending(v => v.TotalArea)
                .ToList();

        private static List<CultivationStatusBreakdown> BuildStatusBreakdown(
            IReadOnlyList<PlotCultivation> plotCultivations) =>
            plotCultivations
                .GroupBy(pc => pc.Status)
                .Select(g =>
                {
                    var count = g.Count();

                    return new CultivationStatusBreakdown
                    {
                        Status = g.Key,
                        Count = count,
                        TotalArea = g.Sum(GetArea),
                        Percentage = plotCultivations.Count > 0 ? (count / (decimal)plotCultivations.Count) * 100 : 0
                    };
                })
                .OrderByDescending(s => s.Count)
                .ToList();
    }
}

