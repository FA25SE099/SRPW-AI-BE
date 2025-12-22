using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Globalization;

namespace RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetSeasonCostAnalysis
{
    public class GetSeasonCostAnalysisQueryHandler :
        IRequestHandler<GetSeasonCostAnalysisQuery, Result<SeasonCostAnalysisResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetSeasonCostAnalysisQueryHandler> _logger;

        public GetSeasonCostAnalysisQueryHandler(
            IUnitOfWork unitOfWork,
            ILogger<GetSeasonCostAnalysisQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<SeasonCostAnalysisResponse>> Handle(
            GetSeasonCostAnalysisQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var season = await _unitOfWork.Repository<Season>().FindAsync(s => s.Id == request.SeasonId);
                if (season == null)
                {
                    return Result<SeasonCostAnalysisResponse>.Failure("Season not found");
                }

                // First get plot IDs for the group if GroupId is specified
                var plotIds = new List<Guid>();
                if (request.GroupId.HasValue)
                {
                    var plots = await _unitOfWork.PlotRepository.GetPlotsForGroupAsync(request.GroupId.Value, cancellationToken);
                    plotIds = plots.Select(p => p.Id).ToList();
                }

                var plotCultivations = await _unitOfWork.Repository<PlotCultivation>().ListAsync(
                    filter: pc => pc.SeasonId == request.SeasonId &&
                                  (request.GroupId == null || plotIds.Contains(pc.PlotId)) &&
                                  (request.ClusterId == null || pc.Plot.GroupPlots.Any(gp => gp.Group.ClusterId == request.ClusterId)) &&
                                  (request.RiceVarietyId == null || pc.RiceVarietyId == request.RiceVarietyId),
                    includeProperties: q => q
                        .Include(pc => pc.Plot)
                            .ThenInclude(p => p.GroupPlots)
                                .ThenInclude(gp => gp.Group)
                        .Include(pc => pc.RiceVariety)
                        .Include(pc => pc.CultivationTasks)
                            .ThenInclude(ct => ct.ProductionPlanTask)
                                .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
                                    .ThenInclude(pptm => pptm.Material)
                        .Include(pc => pc.CultivationTasks)
                            .ThenInclude(ct => ct.CultivationTaskMaterials)
                                .ThenInclude(ctm => ctm.Material));

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

                var allTasks = plotCultivations.SelectMany(pc => pc.CultivationTasks).ToList();
                var totalArea = plotCultivations.Sum(pc => pc.Area ?? pc.Plot.Area);

                var totalPlannedCost = allTasks
                    .SelectMany(ct => ct.ProductionPlanTask.ProductionPlanTaskMaterials)
                    .Sum(pptm => pptm.EstimatedAmount ?? 0);

                var totalActualMaterialCost = allTasks.Sum(ct => ct.ActualMaterialCost);
                var totalActualServiceCost = allTasks.Sum(ct => ct.ActualServiceCost);
                var totalUavCost = uavInvoices.Sum(inv => inv.TotalAmount);
                var totalActualCost = totalActualMaterialCost + totalActualServiceCost + totalUavCost;

                var materialCosts = allTasks
                    .SelectMany(ct => ct.ProductionPlanTask.ProductionPlanTaskMaterials.Select(pptm => new
                    {
                        Task = ct,
                        PlannedMaterial = pptm,
                        ActualMaterial = ct.CultivationTaskMaterials.FirstOrDefault(ctm => ctm.MaterialId == pptm.MaterialId)
                    }))
                    .GroupBy(x => new { x.PlannedMaterial.MaterialId, x.PlannedMaterial.Material.Name, x.PlannedMaterial.Material.Type, x.PlannedMaterial.Material.Unit })
                    .Select(g => new MaterialCostDetail
                    {
                        MaterialId = g.Key.MaterialId,
                        MaterialName = g.Key.Name,
                        MaterialType = g.Key.Type,
                        Unit = g.Key.Unit,
                        PlannedQuantity = g.Sum(x => x.PlannedMaterial.QuantityPerHa),
                        ActualQuantity = g.Sum(x => x.ActualMaterial?.ActualQuantity ?? 0),
                        QuantityVariance = g.Sum(x => x.ActualMaterial?.ActualQuantity ?? 0) - g.Sum(x => x.PlannedMaterial.QuantityPerHa),
                        PlannedCost = g.Sum(x => x.PlannedMaterial.EstimatedAmount ?? 0),
                        ActualCost = g.Sum(x => x.ActualMaterial?.ActualCost ?? 0),
                        CostVariance = g.Sum(x => x.ActualMaterial?.ActualCost ?? 0) - g.Sum(x => x.PlannedMaterial.EstimatedAmount ?? 0),
                        UsageCount = g.Count(x => x.ActualMaterial != null)
                    })
                    .OrderByDescending(m => m.ActualCost)
                    .ToList();

                var costsByTaskType = allTasks
                    .GroupBy(ct => ct.TaskType ?? ct.ProductionPlanTask.TaskType)
                    .Select(g => new TaskTypeCostDetail
                    {
                        TaskType = g.Key,
                        TaskCount = g.Count(),
                        PlannedCost = g.SelectMany(ct => ct.ProductionPlanTask.ProductionPlanTaskMaterials).Sum(pptm => pptm.EstimatedAmount ?? 0),
                        ActualCost = g.Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost),
                        CostVariance = g.Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost) -
                                       g.SelectMany(ct => ct.ProductionPlanTask.ProductionPlanTaskMaterials).Sum(pptm => pptm.EstimatedAmount ?? 0),
                        MaterialCost = g.Sum(ct => ct.ActualMaterialCost),
                        ServiceCost = g.Sum(ct => ct.ActualServiceCost)
                    })
                    .OrderByDescending(t => t.ActualCost)
                    .ToList();

                var costsByVariety = plotCultivations
                    .GroupBy(pc => new { pc.RiceVarietyId, pc.RiceVariety.VarietyName })
                    .Select(g => new RiceVarietyCostDetail
                    {
                        RiceVarietyId = g.Key.RiceVarietyId,
                        VarietyName = g.Key.VarietyName,
                        TotalArea = g.Sum(pc => pc.Area ?? pc.Plot.Area),
                        TotalCost = g.SelectMany(pc => pc.CultivationTasks).Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost),
                        CostPerHectare = g.Sum(pc => pc.Area ?? pc.Plot.Area) > 0
                            ? g.SelectMany(pc => pc.CultivationTasks).Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost) / g.Sum(pc => pc.Area ?? pc.Plot.Area)
                            : 0,
                        CultivationCount = g.Count()
                    })
                    .OrderByDescending(v => v.TotalCost)
                    .ToList();

                var monthlyCosts = allTasks
                    .Where(ct => ct.ActualStartDate.HasValue)
                    .GroupBy(ct => new { ct.ActualStartDate!.Value.Year, ct.ActualStartDate.Value.Month })
                    .Select(g => new MonthlyCostDistribution
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                        MaterialCost = g.Sum(ct => ct.ActualMaterialCost),
                        ServiceCost = g.Sum(ct => ct.ActualServiceCost),
                        TotalCost = g.Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost),
                        TaskCount = g.Count()
                    })
                    .OrderBy(m => m.Year).ThenBy(m => m.Month)
                    .ToList();

                var uavCostDetail = new UavServiceCostDetail
                {
                    TotalInvoiced = uavInvoices.Sum(inv => inv.TotalAmount),
                    TotalPaid = uavInvoices.Where(inv => inv.Status == InvoiceStatus.Paid).Sum(inv => inv.TotalAmount),
                    TotalPending = uavInvoices.Where(inv => inv.Status == InvoiceStatus.Pending || inv.Status == InvoiceStatus.Paid).Sum(inv => inv.TotalAmount),
                    InvoiceCount = uavInvoices.Count,
                    PaidInvoiceCount = uavInvoices.Count(inv => inv.Status == InvoiceStatus.Paid),
                    AverageInvoiceAmount = uavInvoices.Any() ? uavInvoices.Average(inv => inv.TotalAmount) : 0,
                    TotalAreaServiced = uavInvoices.Sum(inv => inv.TotalArea),
                    AverageRatePerHa = uavInvoices.Any() ? uavInvoices.Average(inv => inv.RatePerHa) : 0
                };

                var response = new SeasonCostAnalysisResponse
                {
                    SeasonId = season.Id,
                    SeasonName = season.SeasonName,

                    Overview = new CostOverview
                    {
                        TotalPlannedCost = totalPlannedCost,
                        TotalActualCost = totalActualCost,
                        TotalVariance = totalActualCost - totalPlannedCost,
                        VariancePercentage = totalPlannedCost > 0 ? ((totalActualCost - totalPlannedCost) / totalPlannedCost) * 100 : 0,
                        MaterialCost = totalActualMaterialCost,
                        ServiceCost = totalActualServiceCost,
                        UavCost = totalUavCost,
                        TotalArea = totalArea,
                        CostPerHectare = totalArea > 0 ? totalActualCost / totalArea : 0
                    },

                    MaterialCosts = materialCosts,
                    CostsByTaskType = costsByTaskType,
                    CostsByVariety = costsByVariety,
                    MonthlyCosts = monthlyCosts,
                    UavCosts = uavCostDetail
                };

                _logger.LogInformation(
                    "Retrieved cost analysis for season {SeasonId}: {TotalCost} total actual cost",
                    request.SeasonId, totalActualCost);

                return Result<SeasonCostAnalysisResponse>.Success(
                    response,
                    "Successfully retrieved season cost analysis");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cost analysis for season {SeasonId}", request.SeasonId);
                return Result<SeasonCostAnalysisResponse>.Failure(
                    "An error occurred while retrieving season cost analysis");
            }
        }
    }
}

