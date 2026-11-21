using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.SupervisorFeature.Queries.ViewGroupBySeason;

public class ViewGroupBySeasonQueryHandler 
    : IRequestHandler<ViewGroupBySeasonQuery, Result<List<GroupBySeasonResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ViewGroupBySeasonQueryHandler> _logger;

    public ViewGroupBySeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<ViewGroupBySeasonQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<GroupBySeasonResponse>>> Handle(
        ViewGroupBySeasonQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Verify supervisor exists
            var supervisor = (await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.Id == request.SupervisorId))
                .FirstOrDefault();

            if (supervisor == null)
            {
                return Result<List<GroupBySeasonResponse>>.Failure("Supervisor not found");
            }

            var (targetSeason, targetYear, seasonError) = await ResolveTargetSeasonAsync(request);
            if (targetSeason == null)
            {
                return Result<List<GroupBySeasonResponse>>.Failure(seasonError!);
            }

            var groups = await _unitOfWork.Repository<Group>()
                .GetQueryable()
                .AsNoTracking()
                .Include(g => g.YearSeason)
                    .ThenInclude(ys => ys!.RiceVariety)
                .Include(g => g.Cluster)
                .Where(g =>
                    g.SupervisorId == request.SupervisorId &&
                    g.YearSeason != null && g.YearSeason.SeasonId == targetSeason.Id &&
                    g.Year == targetYear)
                .ToListAsync(cancellationToken);

            if (!groups.Any())
            {
                return Result<List<GroupBySeasonResponse>>.Failure(
                    $"No groups assigned to this supervisor for {targetSeason.SeasonName} {targetYear}");
            }

            bool isPastSeason = IsPastSeason(targetSeason, targetYear);
            bool isCurrentSeason = IsCurrentSeason(targetSeason, targetYear);
            var responses = new List<GroupBySeasonResponse>(groups.Count);

            foreach (var group in groups)
            {
                responses.Add(await BuildGroupResponseAsync(
                    group, targetSeason, targetYear, isCurrentSeason, isPastSeason, cancellationToken));
            }

            _logger.LogInformation(
                "Retrieved {GroupCount} groups for supervisor {SupervisorId} in {Season} {Year}",
                responses.Count, request.SupervisorId, targetSeason.SeasonName, targetYear);

            return Result<List<GroupBySeasonResponse>>.Success(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting groups for supervisor {SupervisorId}",
                request.SupervisorId);
            return Result<List<GroupBySeasonResponse>>.Failure(
                $"Error retrieving groups: {ex.Message}");
        }
    }

    private async Task<(Season? Season, int Year, string? Error)> ResolveTargetSeasonAsync(
        ViewGroupBySeasonQuery request)
    {
        if (request.SeasonId.HasValue && request.Year.HasValue)
        {
            var season = await _unitOfWork.Repository<Season>()
                .FindAsync(s => s.Id == request.SeasonId.Value);

            return season != null
                ? (season, request.Year.Value, null)
                : (null, 0, "Season not found");
        }

        var (currentSeason, currentYear) = await GetCurrentSeasonAndYear();
        return currentSeason != null
            ? (currentSeason, currentYear, null)
            : (null, 0, "No current season could be determined");
    }

    private async Task<GroupBySeasonResponse> BuildGroupResponseAsync(
        Group group,
        Season targetSeason,
        int targetYear,
        bool isCurrentSeason,
        bool isPastSeason,
        CancellationToken cancellationToken)
    {
        var plotsList = (await _unitOfWork.PlotRepository.GetPlotsForGroupAsync(group.Id, cancellationToken)).ToList();

        var farmerIds = plotsList.Select(p => p.FarmerId).Distinct().ToList();
        var farmers = await _unitOfWork.FarmerRepository.ListAsync(f => farmerIds.Contains(f.Id));
        var farmerDict = farmers.ToDictionary(f => f.Id);

        var plansList = (await _unitOfWork.Repository<ProductionPlan>()
            .ListAsync(pp => pp.GroupId == group.Id)).ToList();

        var activePlan = plansList
            .Where(p => p.Status == TaskStatus.Approved || p.Status == TaskStatus.InProgress)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();

        var currentState = DetermineGroupState(isPastSeason, plansList, activePlan);
        var referencePlan = activePlan ?? plansList.FirstOrDefault();

        return new GroupBySeasonResponse
        {
            GroupId = group.Id,
            GroupName = $"Group {group.Id.ToString().Substring(0, 8)}",
            Status = group.Status.ToString(),
            IsCurrentSeason = isCurrentSeason,
            IsPastSeason = isPastSeason,
            CurrentState = currentState,

            Season = new GroupSeasonInfo
            {
                SeasonId = targetSeason.Id,
                SeasonName = targetSeason.SeasonName,
                SeasonType = targetSeason.SeasonType ?? "",
                StartDate = targetSeason.StartDate,
                EndDate = targetSeason.EndDate,
                IsActive = targetSeason.IsActive,
                Year = targetYear
            },

            TotalArea = group.TotalArea,
            AreaGeoJson = PlotGeometryHelper.SerializeGeometry(group.Area),
            PlantingDate = group.PlantingDate,
            RiceVarietyId = group.YearSeason?.RiceVarietyId,
            RiceVarietyName = group.YearSeason?.RiceVariety?.VarietyName,
            ClusterId = group.ClusterId,
            ClusterName = group.Cluster?.ClusterName,

            Plots = MapPlotOverviews(plotsList, farmerDict, currentState == GroupState.PrePlanning),

            Readiness = currentState == GroupState.PrePlanning
                ? CalculateGroupReadiness(group, plotsList)
                : null,

            PlanOverview = referencePlan != null
                ? await CalculatePlanOverview(referencePlan, cancellationToken)
                : null,

            Economics = (isPastSeason || currentState == GroupState.Completed) && referencePlan != null
                ? await CalculateEconomicOverview(plotsList.Select(p => p.Id).ToList(), referencePlan, cancellationToken)
                : null
        };
    }

    #region Season and State Determination

    private async Task<(Season? season, int year)> GetCurrentSeasonAndYear()
    {
        var today = DateTime.Now;
        var allSeasons = await _unitOfWork.Repository<Season>()
            .ListAsync(_ => true);

        foreach (var season in allSeasons)
        {
            if (!SeasonDateHelper.IsDateInSeasonRange(today.Month, today.Day, season.StartDate, season.EndDate))
                continue;

            SeasonDateHelper.TryParseMonthDay(season.StartDate, out int startMonth, out _);

            // If we're before the season start month and season starts late in year,
            // we're in the previous year's season cycle
            int year = today.Month < startMonth && startMonth > 6 ? today.Year - 1 : today.Year;
            return (season, year);
        }

        return (null, today.Year);
    }

    private static bool IsPastSeason(Season season, int year) =>
        SeasonDateHelper.TryGetSeasonDateRange(season, year, out _, out var seasonEndDate) && DateTime.Now > seasonEndDate;

    private static bool IsCurrentSeason(Season season, int year)
    {
        if (!SeasonDateHelper.TryGetSeasonDateRange(season, year, out var seasonStart, out var seasonEnd))
            return false;

        var today = DateTime.Now;
        return today >= seasonStart && today <= seasonEnd;
    }

    private static GroupState DetermineGroupState(
        bool isPastSeason,
        List<ProductionPlan> allPlans,
        ProductionPlan? activePlan)
    {
        if (isPastSeason)
            return GroupState.Archived;

        if (!allPlans.Any())
            return GroupState.PrePlanning;

        if (activePlan is { Status: TaskStatus.Approved or TaskStatus.InProgress })
            return GroupState.InProduction;

        if (allPlans.Any(p => p.Status == TaskStatus.Draft))
            return GroupState.Planning;

        return GroupState.PrePlanning;
    }

    #endregion

    #region Plot Mapping and Readiness

    private static List<PlotOverview> MapPlotOverviews(
        List<Plot> plots,
        Dictionary<Guid, Farmer> farmerDict,
        bool includeReadiness)
    {
        return plots.Select(plot =>
        {
            var farmer = farmerDict.GetValueOrDefault(plot.FarmerId);
            return new PlotOverview
            {
                PlotId = plot.Id,
                SoThua = plot.SoThua,
                SoTo = plot.SoTo,
                Area = plot.Area,
                SoilType = plot.SoilType,
                Status = plot.Status.ToString(),
                HasPolygon = plot.Boundary != null,
                PolygonGeoJson = PlotGeometryHelper.SerializeGeometry(plot.Boundary),
                CentroidGeoJson = PlotGeometryHelper.SerializeGeometry(plot.Coordinate),
                FarmerId = plot.FarmerId,
                FarmerName = farmer?.FullName,
                FarmerPhone = farmer?.PhoneNumber,
                FarmerAddress = farmer?.Address,
                FarmCode = farmer?.FarmCode,
                Readiness = includeReadiness ? CalculatePlotReadiness(plot, farmer) : null
            };
        })
        .OrderBy(p => p.SoThua)
        .ThenBy(p => p.SoTo)
        .ToList();
    }

    private static PlotReadinessInfo CalculatePlotReadiness(Plot plot, Farmer? farmer)
    {
        var blocking = new List<string>();
        var warnings = new List<string>();

        if (plot.Boundary == null)
            blocking.Add("Missing polygon boundary");

        if (plot.Area <= 0)
            blocking.Add("Invalid or zero area");

        if (farmer == null || string.IsNullOrEmpty(farmer.FullName) || string.IsNullOrEmpty(farmer.PhoneNumber))
            warnings.Add("Incomplete farmer information");

        if (string.IsNullOrEmpty(plot.SoilType))
            warnings.Add("Soil type not specified");

        switch (plot.Status)
        {
            case PlotStatus.Active:
                break;
            case PlotStatus.Emergency:
                blocking.Add("Plot is in Emergency status");
                break;
            case PlotStatus.PendingPolygon:
                blocking.Add("Polygon assignment pending");
                break;
            default:
                warnings.Add($"Plot status is {plot.Status}");
                break;
        }

        bool isReady = blocking.Count == 0;
        string readinessLevel = (isReady, warnings.Count) switch
        {
            (true, 0) => "Ready",
            (true, _) => "Warning",
            _ => "Blocked"
        };

        return new PlotReadinessInfo
        {
            IsReady = isReady,
            ReadinessLevel = readinessLevel,
            BlockingIssues = blocking,
            Warnings = warnings
        };
    }

    private static GroupReadinessOverview CalculateGroupReadiness(Group group, List<Plot> plots)
    {
        int plotsWithPolygon = plots.Count(p => p.Boundary != null);
        var readiness = new GroupReadinessOverview
        {
            TotalPlots = plots.Count,
            PlotsWithPolygon = plotsWithPolygon,
            PlotsWithoutPolygon = plots.Count - plotsWithPolygon
        };

        var blocking = new List<string>();
        var warnings = new List<string>();

        readiness.HasRiceVariety = group.YearSeason?.RiceVarietyId != null;
        if (!readiness.HasRiceVariety)
            blocking.Add("Rice variety not selected");

        readiness.HasTotalArea = group.TotalArea.HasValue && group.TotalArea.Value > 0;
        if (!readiness.HasTotalArea)
            blocking.Add("Total area not defined");

        readiness.HasPlots = plots.Any();
        if (!readiness.HasPlots)
            blocking.Add("No plots assigned to this group");

        int readyPlotsCount = plots.Count(p => CalculatePlotReadiness(p, null).IsReady);
        int plotsWithIssuesCount = plots.Count - readyPlotsCount;

        readiness.ReadyPlots = readyPlotsCount;
        readiness.PlotsWithIssues = plotsWithIssuesCount;
        readiness.AllPlotsReady = plots.Any() && plotsWithIssuesCount == 0;

        if (!readiness.AllPlotsReady && plots.Any())
            blocking.Add($"{plotsWithIssuesCount} plot(s) have blocking issues or missing data");

        if (group.Status != GroupStatus.Active && group.Status != GroupStatus.ReadyForOptimization)
            warnings.Add($"Group status is {group.Status}");

        if (!group.PlantingDate.HasValue)
            warnings.Add("No planting date set (can be specified when creating production plan)");

        readiness.BlockingIssues = blocking;
        readiness.Warnings = warnings;

        const int totalRequired = 4;
        int passed = (readiness.HasRiceVariety ? 1 : 0)
                   + (readiness.HasTotalArea ? 1 : 0)
                   + (readiness.HasPlots ? 1 : 0)
                   + (readiness.AllPlotsReady ? 1 : 0);

        readiness.ReadinessScore = (int)((passed / (double)totalRequired) * 100);
        readiness.IsReady = blocking.Count == 0;
        readiness.ReadinessLevel = readiness.IsReady
            ? "Ready"
            : readiness.ReadinessScore switch
            {
                >= 75 => "Almost Ready",
                >= 50 => "In Progress",
                _ => "Not Ready"
            };

        return readiness;
    }

    #endregion

    #region Plan Overview (Lightweight)

    private async Task<ProductionPlanOverview> CalculatePlanOverview(ProductionPlan plan, CancellationToken cancellationToken)
    {
        var stagesList = await _unitOfWork.Repository<ProductionStage>()
            .GetQueryable()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.ProductionPlanTasks)
                .ThenInclude(t => t.CultivationTasks)
            .Where(s => s.ProductionPlanId == plan.Id)
            .OrderBy(s => s.SequenceOrder)
            .ToListAsync(cancellationToken);

        int totalTasks = 0;
        int completedTasks = 0;
        int inProgressTasks = 0;
        int contingencyTasks = 0;
        int completedStages = 0;
        int inProgressStages = 0;

        decimal estimatedTotalCost = 0;
        decimal actualCostToDate = 0;
        bool hasActiveIssues = false;

        foreach (var stage in stagesList)
        {
            var stageTasks = stage.ProductionPlanTasks.ToList();
            var allCultivationTasks = stageTasks
                .SelectMany(pt => pt.CultivationTasks)
                .ToList();

            int stageCompleted = allCultivationTasks.Count(ct => ct.Status == TaskStatus.Completed);
            int stageInProgress = allCultivationTasks.Count(ct => ct.Status == TaskStatus.InProgress);

            totalTasks += allCultivationTasks.Count;
            completedTasks += stageCompleted;
            inProgressTasks += stageInProgress;
            contingencyTasks += allCultivationTasks.Count(ct => ct.IsContingency);

            if (allCultivationTasks.Count > 0)
            {
                if (stageCompleted == allCultivationTasks.Count)
                    completedStages++;
                else if (stageInProgress > 0 || stageCompleted > 0)
                    inProgressStages++;
            }

            estimatedTotalCost += stageTasks.Sum(t => t.EstimatedMaterialCost);
            actualCostToDate += allCultivationTasks.Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost);

            if (!hasActiveIssues && allCultivationTasks.Any(ct => ct.IsContingency || !string.IsNullOrEmpty(ct.InterruptionReason)))
            {
                hasActiveIssues = true;
            }
        }

        return new ProductionPlanOverview
        {
            ProductionPlanId = plan.Id,
            PlanName = plan.PlanName,
            Status = plan.Status.ToString(),
            BasePlantingDate = plan.BasePlantingDate,
            SubmittedAt = plan.SubmittedAt,
            ApprovedAt = plan.ApprovedAt,

            TotalStages = stagesList.Count,
            CompletedStages = completedStages,
            InProgressStages = inProgressStages,
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            InProgressTasks = inProgressTasks,
            OverallProgressPercentage = totalTasks > 0 ? (completedTasks / (decimal)totalTasks) * 100 : 0,

            DaysElapsed = (DateTime.Now - plan.BasePlantingDate).Days,
            EstimatedTotalDays = 0,
            IsOnSchedule = true,
            DaysBehindSchedule = null,

            EstimatedTotalCost = estimatedTotalCost,
            ActualCostToDate = actualCostToDate,
            CostVariancePercentage = estimatedTotalCost > 0
                ? ((actualCostToDate - estimatedTotalCost) / estimatedTotalCost) * 100
                : 0,

            ContingencyTasksCount = contingencyTasks,
            HasActiveIssues = hasActiveIssues,
            HasDetailedProgress = true
        };
    }

    #endregion

    #region Economic Overview (Lightweight)

    private async Task<EconomicOverview> CalculateEconomicOverview(List<Guid> plotIds, ProductionPlan plan, CancellationToken cancellationToken)
    {
        var plotCultivationsList = await _unitOfWork.Repository<PlotCultivation>()
            .GetQueryable()
            .AsNoTracking()
            .Where(pc => plotIds.Contains(pc.PlotId))
            .Select(pc => new
            {
                pc.ActualYield,
                pc.ExpectedYield,
                EffectiveArea = pc.Area ?? pc.Plot.Area,
                ActualMaterialCost = pc.CultivationTasks.Sum(t => (decimal?)t.ActualMaterialCost) ?? 0m,
                ActualServiceCost = pc.CultivationTasks.Sum(t => (decimal?)t.ActualServiceCost) ?? 0m
            })
            .ToListAsync(cancellationToken);

        decimal actualMaterialCost = plotCultivationsList.Sum(pc => pc.ActualMaterialCost);
        decimal actualServiceCost = plotCultivationsList.Sum(pc => pc.ActualServiceCost);
        decimal totalActualCost = actualMaterialCost + actualServiceCost;

        decimal totalEstimatedCost = (await _unitOfWork.Repository<ProductionStage>()
            .GetQueryable()
            .AsNoTracking()
            .Where(s => s.ProductionPlanId == plan.Id)
            .SelectMany(s => s.ProductionPlanTasks)
            .SumAsync(t => (decimal?)t.EstimatedMaterialCost, cancellationToken)) ?? 0m;

        decimal totalActualYield = plotCultivationsList.Sum(pc => pc.ActualYield ?? 0);
        decimal totalExpectedYield = plotCultivationsList.Sum(pc => pc.ExpectedYield ?? 0);

        decimal totalArea = plotCultivationsList.Sum(pc => pc.EffectiveArea);
        decimal yieldPerHa = totalArea > 0 ? totalActualYield / totalArea : 0;

        // Placeholder rice price (VND per kg)
        const decimal estimatedRicePrice = 7000;
        decimal totalRevenue = totalActualYield * estimatedRicePrice;
        decimal grossProfit = totalRevenue - totalActualCost;
        decimal profitMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0;
        decimal roi = totalActualCost > 0 ? (grossProfit / totalActualCost) * 100 : 0;

        decimal costPerKg = totalActualYield > 0 ? totalActualCost / totalActualYield : 0;
        decimal costPerHa = totalArea > 0 ? totalActualCost / totalArea : 0;

        int performanceScore = CalculatePerformanceScore(yieldPerHa, profitMargin);
        string performanceRating = performanceScore switch
        {
            >= 80 => "Excellent",
            >= 60 => "Good",
            >= 40 => "Average",
            _ => "Below Average"
        };

        return new EconomicOverview
        {
            TotalEstimatedCost = totalEstimatedCost,
            TotalActualCost = totalActualCost,
            CostVariance = totalActualCost - totalEstimatedCost,
            CostVariancePercentage = totalEstimatedCost > 0
                ? ((totalActualCost - totalEstimatedCost) / totalEstimatedCost) * 100
                : 0,

            ActualMaterialCost = actualMaterialCost,
            ActualServiceCost = actualServiceCost,

            ExpectedYield = totalExpectedYield,
            ActualYield = totalActualYield,
            YieldVariance = totalActualYield - totalExpectedYield,
            YieldVariancePercentage = totalExpectedYield > 0
                ? ((totalActualYield - totalExpectedYield) / totalExpectedYield) * 100
                : 0,
            YieldPerHectare = yieldPerHa,

            GrossProfit = grossProfit,
            ProfitMargin = profitMargin,
            ReturnOnInvestment = roi,

            CostPerKg = costPerKg,
            CostPerHectare = costPerHa,

            PerformanceRating = performanceRating,
            PerformanceScore = performanceScore,
            HasDetailedEconomics = true
        };
    }

    private static int CalculatePerformanceScore(decimal yieldPerHa, decimal profitMargin)
    {
        int score = 50;

        score += yieldPerHa switch
        {
            >= 7000 => 30,
            >= 6000 => 20,
            >= 5000 => 10,
            _ => 0
        };

        score += profitMargin switch
        {
            >= 30 => 20,
            >= 20 => 15,
            >= 10 => 10,
            _ => 0
        };

        return Math.Clamp(score, 0, 100);
    }

    #endregion
}

