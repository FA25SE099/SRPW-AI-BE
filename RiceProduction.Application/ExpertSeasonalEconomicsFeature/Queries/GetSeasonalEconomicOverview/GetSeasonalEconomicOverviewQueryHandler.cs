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

                var plotCultivations = await _unitOfWork.Repository<PlotCultivation>().ListAsync(
                    filter: pc => pc.SeasonId == request.SeasonId &&
                                  (request.GroupId == null || pc.Plot.GroupId == request.GroupId) &&
                                  (request.ClusterId == null || pc.Plot.Group!.ClusterId == request.ClusterId),
                    includeProperties: q => q
                        .Include(pc => pc.Plot)
                            .ThenInclude(p => p.Farmer)
                        .Include(pc => pc.Plot)
                            .ThenInclude(p => p.Group!)
                                .ThenInclude(g => g.Cluster)
                        .Include(pc => pc.RiceVariety)
                        .Include(pc => pc.CultivationTasks)
                            .ThenInclude(ct => ct.CultivationTaskMaterials)
                                .ThenInclude(ctm => ctm.Material));

                var riceVarietySeasons = await _unitOfWork.Repository<RiceVarietySeason>().ListAsync(
                    filter: rvs => rvs.SeasonId == request.SeasonId);

                var uavInvoices = await _unitOfWork.Repository<UavInvoice>().ListAsync(
                    filter: inv => inv.UavServiceOrder != null &&
                                   inv.UavServiceOrder.Group!.SeasonId == request.SeasonId &&
                                   (request.GroupId == null || inv.UavServiceOrder.GroupId == request.GroupId) &&
                                   (request.ClusterId == null || inv.UavServiceOrder.Group.ClusterId == request.ClusterId),
                    includeProperties: q => q
                        .Include(inv => inv.UavServiceOrder!)
                            .ThenInclude(uso => uso.Group));

                var totalArea = plotCultivations.Sum(pc => pc.Area ?? pc.Plot.Area);
                var uniqueFarmers = plotCultivations.Select(pc => pc.Plot.FarmerId).Distinct().Count();
                var uniqueGroups = plotCultivations.Where(pc => pc.Plot.GroupId.HasValue)
                    .Select(pc => pc.Plot.GroupId!.Value).Distinct().Count();

                var allTasks = plotCultivations.SelectMany(pc => pc.CultivationTasks).ToList();
                var totalTasks = allTasks.Count;
                var completedTasks = allTasks.Count(ct => ct.Status == TaskStatus.Completed);

                var totalActualMaterialCost = allTasks.Sum(ct => ct.ActualMaterialCost);
                var totalActualServiceCost = allTasks.Sum(ct => ct.ActualServiceCost);
                var totalUavCost = uavInvoices.Where(inv => inv.Status == InvoiceStatus.Paid || inv.Status == InvoiceStatus.Paid)
                    .Sum(inv => inv.TotalAmount);
                var totalActualCost = totalActualMaterialCost + totalActualServiceCost + totalUavCost;

                var totalEstimatedCost = plotCultivations
                    .SelectMany(pc => pc.CultivationTasks)
                    .SelectMany(ct => ct.ProductionPlanTask.ProductionPlanTaskMaterials)
                    .Sum(pptm => pptm.EstimatedAmount ?? 0);

                var totalExpectedYield = plotCultivations.Sum(pc =>
                {
                    var varietySeason = riceVarietySeasons.FirstOrDefault(rvs => rvs.RiceVarietyId == pc.RiceVarietyId);
                    var expectedYieldPerHa = varietySeason?.ExpectedYieldPerHectare ?? 0;
                    var area = pc.Area ?? pc.Plot.Area;
                    return expectedYieldPerHa * area;
                });

                var totalActualYield = plotCultivations.Sum(pc => pc.ActualYield ?? 0);
                var harvestedCount = plotCultivations.Count(pc => pc.Status == CultivationStatus.Completed);
                var pendingHarvestCount = plotCultivations.Count(pc =>
                    pc.Status == CultivationStatus.InProgress || pc.Status == CultivationStatus.InProgress);

                var varietyDistribution = plotCultivations
                    .GroupBy(pc => new { pc.RiceVarietyId, pc.RiceVariety.VarietyName })
                    .Select(g => new RiceVarietyDistribution
                    {
                        RiceVarietyId = g.Key.RiceVarietyId,
                        VarietyName = g.Key.VarietyName,
                        CultivationCount = g.Count(),
                        TotalArea = g.Sum(pc => pc.Area ?? pc.Plot.Area),
                        Percentage = totalArea > 0 ? (g.Sum(pc => pc.Area ?? pc.Plot.Area) / totalArea) * 100 : 0
                    })
                    .OrderByDescending(v => v.TotalArea)
                    .ToList();

                var statusBreakdown = plotCultivations
                    .GroupBy(pc => pc.Status)
                    .Select(g => new CultivationStatusBreakdown
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        TotalArea = g.Sum(pc => pc.Area ?? pc.Plot.Area),
                        Percentage = plotCultivations.Count > 0 ? (g.Count() / (decimal)plotCultivations.Count) * 100 : 0
                    })
                    .OrderByDescending(s => s.Count)
                    .ToList();

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

                    VarietyDistribution = varietyDistribution,
                    StatusBreakdown = statusBreakdown
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
    }
}

