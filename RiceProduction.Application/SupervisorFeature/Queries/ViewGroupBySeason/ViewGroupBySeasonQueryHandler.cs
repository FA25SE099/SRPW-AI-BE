using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
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

            Season targetSeason;
            int targetYear;

            if (request.SeasonId.HasValue && request.Year.HasValue)
            {
                targetSeason = await _unitOfWork.Repository<Season>()
                    .FindAsync(s => s.Id == request.SeasonId.Value);
                
                if (targetSeason == null)
                {
                    return Result<List<GroupBySeasonResponse>>.Failure("Season not found");
                }
                
                targetYear = request.Year.Value;
            }
            else
            {
                var currentSeasonResult = await GetCurrentSeasonAndYear();
                if (currentSeasonResult.season == null)
                {
                    return Result<List<GroupBySeasonResponse>>.Failure("No current season could be determined");
                }
                
                targetSeason = currentSeasonResult.season;
                targetYear = currentSeasonResult.year;
            }

            var groups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => 
                    g.SupervisorId == request.SupervisorId && 
                    g.SeasonId == targetSeason.Id &&
                    g.Year == targetYear);

            if (!groups.Any())
            {
                return Result<List<GroupBySeasonResponse>>.Failure(
                    $"No groups assigned to this supervisor for {targetSeason.SeasonName} {targetYear}");
            }

            var responses = new List<GroupBySeasonResponse>();
            bool isPastSeason = IsPastSeason(targetSeason, targetYear);
            bool isCurrentSeason = IsCurrentSeason(targetSeason, targetYear);

            foreach (var group in groups)
            {
                var plots = await _unitOfWork.Repository<Plot>()
                    .ListAsync(p => p.GroupId == group.Id);
                var plotsList = plots.ToList();

                var farmerIds = plotsList.Select(p => p.FarmerId).Distinct().ToList();
                var farmers = await _unitOfWork.FarmerRepository
                    .ListAsync(f => farmerIds.Contains(f.Id));
                var farmerDict = farmers.ToDictionary(f => f.Id);

                RiceVariety? riceVariety = null;
                if (group.RiceVarietyId.HasValue)
                {
                    riceVariety = await _unitOfWork.Repository<RiceVariety>()
                        .FindAsync(rv => rv.Id == group.RiceVarietyId.Value);
                }

                var cluster = await _unitOfWork.Repository<Cluster>()
                    .FindAsync(c => c.Id == group.ClusterId);

                var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
                    .ListAsync(pp => pp.GroupId == group.Id);
                var plansList = productionPlans.ToList();
                
                var activePlan = plansList
                    .Where(p => p.Status == TaskStatus.Approved || p.Status == TaskStatus.InProgress)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();

                GroupState currentState = DetermineGroupState(
                    isCurrentSeason, 
                    isPastSeason, 
                    plansList, 
                    activePlan);

                var response = new GroupBySeasonResponse
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
                    AreaGeoJson = group.Area != null ? SerializeGeometry(group.Area) : null,
                    PlantingDate = group.PlantingDate,
                    RiceVarietyId = group.RiceVarietyId,
                    RiceVarietyName = riceVariety?.VarietyName,
                    ClusterId = group.ClusterId,
                    ClusterName = cluster?.ClusterName,
                    
                    Plots = MapPlotOverviews(plotsList, farmerDict, currentState == GroupState.PrePlanning),
                    
                    Readiness = currentState == GroupState.PrePlanning
                        ? CalculateGroupReadiness(group, plotsList, plansList)
                        : null,
                    
                    PlanOverview = (activePlan != null || plansList.Any())
                        ? await CalculatePlanOverview(group.Id, activePlan ?? plansList.First())
                        : null,
                    
                    Economics = (isPastSeason || currentState == GroupState.Completed) && plansList.Any()
                        ? await CalculateEconomicOverview(group.Id, activePlan ?? plansList.First())
                        : null
                };

                responses.Add(response);
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

    #region Season and State Determination

    private async Task<(Season? season, int year)> GetCurrentSeasonAndYear()
    {
        var today = DateTime.Now;
        var currentMonth = today.Month;
        var currentDay = today.Day;
        
        var allSeasons = await _unitOfWork.Repository<Season>()
            .ListAsync(_ => true);
        
        foreach (var season in allSeasons)
        {
            if (IsDateInSeasonRange(currentMonth, currentDay, season.StartDate, season.EndDate))
            {
                // Determine year based on season dates
                var startParts = season.StartDate.Split('/');
                int startMonth = int.Parse(startParts[0]);
                
                // If we're before the season start month and season starts late in year, 
                // we're in the previous year's season cycle
                int year = today.Year;
                if (currentMonth < startMonth && startMonth > 6)
                {
                    year--;
                }
                
                return (season, year);
            }
        }
        
        return (null, today.Year);
    }

    private bool IsDateInSeasonRange(int month, int day, string startDateStr, string endDateStr)
    {
        try
        {
            var startParts = startDateStr.Split('/');
            var endParts = endDateStr.Split('/');
            
            int startMonth = int.Parse(startParts[0]);
            int startDay = int.Parse(startParts[1]);
            int endMonth = int.Parse(endParts[0]);
            int endDay = int.Parse(endParts[1]);
            
            int currentDate = month * 100 + day;
            int seasonStart = startMonth * 100 + startDay;
            int seasonEnd = endMonth * 100 + endDay;
            
            // Handle cross-year seasons (e.g., Dec to Mar)
            if (seasonStart > seasonEnd)
            {
                return currentDate >= seasonStart || currentDate <= seasonEnd;
            }
            else
            {
                return currentDate >= seasonStart && currentDate <= seasonEnd;
            }
        }
        catch
        {
            return false;
        }
    }

    private bool IsPastSeason(Season season, int year)
    {
        var today = DateTime.Now;
        
        try
        {
            var endParts = season.EndDate.Split('/');
            int endMonth = int.Parse(endParts[0]);
            int endDay = int.Parse(endParts[1]);
            
            // Determine actual end year
            var startParts = season.StartDate.Split('/');
            int startMonth = int.Parse(startParts[0]);
            int actualEndYear = year;
            
            // If season crosses year boundary
            if (endMonth < startMonth)
            {
                actualEndYear = year + 1;
            }
            
            var seasonEndDate = new DateTime(actualEndYear, endMonth, endDay);
            return today > seasonEndDate;
        }
        catch
        {
            return false;
        }
    }

    private bool IsCurrentSeason(Season season, int year)
    {
        var today = DateTime.Now;
        
        try
        {
            var startParts = season.StartDate.Split('/');
            var endParts = season.EndDate.Split('/');
            
            int startMonth = int.Parse(startParts[0]);
            int startDay = int.Parse(startParts[1]);
            int endMonth = int.Parse(endParts[0]);
            int endDay = int.Parse(endParts[1]);
            
            int actualEndYear = year;
            if (endMonth < startMonth)
            {
                actualEndYear = year + 1;
            }
            
            var seasonStart = new DateTime(year, startMonth, startDay);
            var seasonEnd = new DateTime(actualEndYear, endMonth, endDay);
            
            return today >= seasonStart && today <= seasonEnd;
        }
        catch
        {
            return false;
        }
    }

    private GroupState DetermineGroupState(
        bool isCurrentSeason,
        bool isPastSeason,
        List<ProductionPlan> allPlans,
        ProductionPlan? activePlan)
    {
        // Past season - always archived
        if (isPastSeason)
        {
            return GroupState.Archived;
        }
        
        // No plans at all - pre-planning
        if (!allPlans.Any())
        {
            return GroupState.PrePlanning;
        }
        
        // Has active/approved plan
        if (activePlan != null)
        {
            if (activePlan.Status == TaskStatus.Approved || activePlan.Status == TaskStatus.InProgress)
            {
                // Check if completed (simplified - detailed check in separate endpoint)
                // For now, if approved, assume InProduction
                return GroupState.InProduction;
            }
        }
        
        // Has plans but none approved yet (draft, submitted, pending)
        if (allPlans.Any(p => p.Status == TaskStatus.Draft))
        {
            return GroupState.Planning;
        }
        
        // Fallback
        return GroupState.PrePlanning;
    }

    #endregion

    #region Plot Mapping and Readiness

    private List<PlotOverview> MapPlotOverviews(
        List<Plot> plots, 
        Dictionary<Guid, Farmer> farmerDict,
        bool includeReadiness)
    {
        return plots.Select(plot =>
        {
            var plotOverview = new PlotOverview
            {
                PlotId = plot.Id,
                SoThua = plot.SoThua,
                SoTo = plot.SoTo,
                Area = plot.Area,
                SoilType = plot.SoilType,
                Status = plot.Status.ToString(),
                HasPolygon = plot.Boundary != null,
                PolygonGeoJson = plot.Boundary != null ? SerializeGeometry(plot.Boundary) : null,
                CentroidGeoJson = plot.Coordinate != null ? SerializeGeometry(plot.Coordinate) : null,
                FarmerId = plot.FarmerId,
                FarmerName = farmerDict.GetValueOrDefault(plot.FarmerId)?.FullName,
                FarmerPhone = farmerDict.GetValueOrDefault(plot.FarmerId)?.PhoneNumber,
                FarmerAddress = farmerDict.GetValueOrDefault(plot.FarmerId)?.Address,
                FarmCode = farmerDict.GetValueOrDefault(plot.FarmerId)?.FarmCode
            };
            
            // Add readiness info if in pre-planning state
            if (includeReadiness)
            {
                plotOverview.Readiness = CalculatePlotReadiness(plot, farmerDict.GetValueOrDefault(plot.FarmerId));
            }
            
            return plotOverview;
        })
        .OrderBy(p => p.SoThua)
        .ThenBy(p => p.SoTo)
        .ToList();
    }

    private PlotReadinessInfo CalculatePlotReadiness(Plot plot, Farmer? farmer)
    {
        var blocking = new List<string>();
        var warnings = new List<string>();
        
        // Check 1: Has Polygon
        bool hasPolygon = plot.Boundary != null;
        if (!hasPolygon)
        {
            blocking.Add("Missing polygon boundary");
        }
        
        // Check 2: Valid Area
        bool hasValidArea = plot.Area > 0;
        if (!hasValidArea)
        {
            blocking.Add("Invalid or zero area");
        }
        
        // Check 3: Farmer Info
        bool hasFarmerInfo = farmer != null && 
                            !string.IsNullOrEmpty(farmer.FullName) &&
                            !string.IsNullOrEmpty(farmer.PhoneNumber);
        if (!hasFarmerInfo)
        {
            warnings.Add("Incomplete farmer information");
        }
        
        // Check 4: Soil Type
        bool hasSoilType = !string.IsNullOrEmpty(plot.SoilType);
        if (!hasSoilType)
        {
            warnings.Add("Soil type not specified");
        }
        
        // Check 5: Active Status
        bool isActiveStatus = plot.Status == PlotStatus.Active;
        if (!isActiveStatus)
        {
            if (plot.Status == PlotStatus.Emergency)
                blocking.Add($"Plot is in Emergency status");
            else if (plot.Status == PlotStatus.PendingPolygon)
                blocking.Add("Polygon assignment pending");
            else
                warnings.Add($"Plot status is {plot.Status}");
        }
        
        bool isReady = blocking.Count == 0;
        string readinessLevel;
        
        if (isReady && warnings.Count == 0)
            readinessLevel = "Ready";
        else if (isReady && warnings.Count > 0)
            readinessLevel = "Warning";
        else
            readinessLevel = "Blocked";
        
        return new PlotReadinessInfo
        {
            IsReady = isReady,
            ReadinessLevel = readinessLevel,
            BlockingIssues = blocking,
            Warnings = warnings
        };
    }

    private GroupReadinessOverview CalculateGroupReadiness(
        Group group, 
        List<Plot> plots, 
        List<ProductionPlan> productionPlans)
    {
        var readiness = new GroupReadinessOverview
        {
            TotalPlots = plots.Count,
            PlotsWithPolygon = plots.Count(p => p.Boundary != null),
            PlotsWithoutPolygon = plots.Count(p => p.Boundary == null)
        };

        var blocking = new List<string>();
        var warnings = new List<string>();

        // Check 1: Rice Variety
        readiness.HasRiceVariety = group.RiceVarietyId.HasValue;
        if (!readiness.HasRiceVariety)
        {
            blocking.Add("Rice variety not selected");
        }

        // Check 2: Total Area
        readiness.HasTotalArea = group.TotalArea.HasValue && group.TotalArea.Value > 0;
        if (!readiness.HasTotalArea)
        {
            blocking.Add("Total area not defined");
        }

        // Check 3: Has Plots
        readiness.HasPlots = plots.Any();
        if (!readiness.HasPlots)
        {
            blocking.Add("No plots assigned to this group");
        }

        // Check 4: All Plots Ready
        int readyPlotsCount = 0;
        int plotsWithIssuesCount = 0;
        
        foreach (var plot in plots)
        {
            var plotReadiness = CalculatePlotReadiness(plot, null);
            if (plotReadiness.IsReady)
            {
                readyPlotsCount++;
            }
            else
            {
                plotsWithIssuesCount++;
            }
        }
        
        readiness.ReadyPlots = readyPlotsCount;
        readiness.PlotsWithIssues = plotsWithIssuesCount;
        readiness.AllPlotsReady = plots.Any() && plotsWithIssuesCount == 0;
        
        if (!readiness.AllPlotsReady && plots.Any())
        {
            blocking.Add($"{plotsWithIssuesCount} plot(s) have blocking issues or missing data");
        }

        // Warnings
        if (group.Status != GroupStatus.Active && group.Status != GroupStatus.ReadyForOptimization)
        {
            warnings.Add($"Group status is {group.Status}");
        }

        if (!group.PlantingDate.HasValue)
        {
            warnings.Add("No planting date set (can be specified when creating production plan)");
        }

        readiness.BlockingIssues = blocking;
        readiness.Warnings = warnings;

        // Calculate score
        int totalRequired = 4; // variety, area, plots, all plots ready
        int passed = 0;
        if (readiness.HasRiceVariety) passed++;
        if (readiness.HasTotalArea) passed++;
        if (readiness.HasPlots) passed++;
        if (readiness.AllPlotsReady) passed++;

        readiness.ReadinessScore = (int)((passed / (double)totalRequired) * 100);
        readiness.IsReady = blocking.Count == 0;

        // Determine level
        if (readiness.IsReady)
            readiness.ReadinessLevel = "Ready";
        else if (readiness.ReadinessScore >= 75)
            readiness.ReadinessLevel = "Almost Ready";
        else if (readiness.ReadinessScore >= 50)
            readiness.ReadinessLevel = "In Progress";
        else
            readiness.ReadinessLevel = "Not Ready";

        return readiness;
    }

    #endregion

    #region Plan Overview (Lightweight)

    private async Task<ProductionPlanOverview> CalculatePlanOverview(Guid groupId, ProductionPlan plan)
    {
        // Load stages with minimal data (no task details)
        var stages = await _unitOfWork.Repository<ProductionStage>()
            .ListAsync(
                s => s.ProductionPlanId == plan.Id,
                includeProperties: q => q
                    .Include(s => s.ProductionPlanTasks)
                        .ThenInclude(t => t.CultivationTasks));
        
        var stagesList = stages.OrderBy(s => s.SequenceOrder).ToList();
        
        // Aggregate counts
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
            int stageContingency = allCultivationTasks.Count(ct => ct.IsContingency);
            
            totalTasks += allCultivationTasks.Count;
            completedTasks += stageCompleted;
            inProgressTasks += stageInProgress;
            contingencyTasks += stageContingency;
            
            // Stage status
            if (allCultivationTasks.Count > 0)
            {
                if (stageCompleted == allCultivationTasks.Count)
                    completedStages++;
                else if (stageInProgress > 0 || stageCompleted > 0)
                    inProgressStages++;
            }
            
            // Costs
            estimatedTotalCost += stageTasks.Sum(t => t.EstimatedMaterialCost);
            actualCostToDate += allCultivationTasks.Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost);
            
            // Check for active issues
            if (allCultivationTasks.Any(ct => ct.IsContingency || !string.IsNullOrEmpty(ct.InterruptionReason)))
            {
                hasActiveIssues = true;
            }
        }
        
        // Calculate time tracking
        var daysElapsed = (DateTime.Now - plan.BasePlantingDate).Days;
        //var estimatedTotalDays = stagesList.Any() 
        //    ? (int)(stagesList.Max(s => s.EndDate) - plan.BasePlantingDate).TotalDays 
        //    : 0;
        var estimatedTotalDays = 0;
        // Simple on-schedule check: if more than 50% time passed, should have >50% tasks done
        bool isOnSchedule = true;
        int? daysBehindSchedule = null;
        
        if (estimatedTotalDays > 0 && totalTasks > 0)
        {
            decimal expectedProgress = ((decimal)daysElapsed / estimatedTotalDays) * 100;
            decimal actualProgress = ((decimal)completedTasks / totalTasks) * 100;
            
            if (actualProgress < expectedProgress - 10) // 10% tolerance
            {
                isOnSchedule = false;
                daysBehindSchedule = (int)((expectedProgress - actualProgress) / 100 * estimatedTotalDays);
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
            
            DaysElapsed = daysElapsed,
            EstimatedTotalDays = estimatedTotalDays,
            IsOnSchedule = isOnSchedule,
            DaysBehindSchedule = daysBehindSchedule,
            
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

    private async Task<EconomicOverview> CalculateEconomicOverview(Guid groupId, ProductionPlan plan)
    {
        // Load plot cultivations for this group
        var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
            .ListAsync(
                pc => pc.Plot.GroupId == groupId,
                includeProperties: q => q
                    .Include(pc => pc.CultivationTasks)
                    .Include(pc => pc.Plot));
        
        var plotCultivationsList = plotCultivations.ToList();
        
        // Calculate costs
        var allTasks = plotCultivationsList
            .SelectMany(pc => pc.CultivationTasks)
            .ToList();
        
        decimal actualMaterialCost = allTasks.Sum(t => t.ActualMaterialCost);
        decimal actualServiceCost = allTasks.Sum(t => t.ActualServiceCost);
        decimal totalActualCost = actualMaterialCost + actualServiceCost;
        
        // Estimated cost from plan
        var stages = await _unitOfWork.Repository<ProductionStage>()
            .ListAsync(
                s => s.ProductionPlanId == plan.Id,
                includeProperties: q => q.Include(s => s.ProductionPlanTasks));
        
        decimal totalEstimatedCost = stages
            .SelectMany(s => s.ProductionPlanTasks)
            .Sum(t => t.EstimatedMaterialCost);
        
        // Calculate yield
        decimal totalActualYield = plotCultivationsList.Sum(pc => pc.ActualYield ?? 0);
        decimal totalExpectedYield = plotCultivationsList.Sum(pc => pc.ExpectedYield ?? 0);
        
        decimal totalArea = plotCultivationsList.Sum(pc => pc.Area ?? pc.Plot.Area);
        decimal yieldPerHa = totalArea > 0 ? totalActualYield / totalArea : 0;
        
        // Financial calculations (simplified - would need rice price data)
        decimal estimatedRicePrice = 7000; // VND per kg (placeholder)
        decimal totalRevenue = totalActualYield * estimatedRicePrice;
        decimal grossProfit = totalRevenue - totalActualCost;
        decimal profitMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0;
        decimal roi = totalActualCost > 0 ? (grossProfit / totalActualCost) * 100 : 0;
        
        // Efficiency metrics
        decimal costPerKg = totalActualYield > 0 ? totalActualCost / totalActualYield : 0;
        decimal costPerHa = totalArea > 0 ? totalActualCost / totalArea : 0;
        
        // Performance rating (simplified)
        int performanceScore = CalculatePerformanceScore(
            yieldPerHa,
            costPerHa,
            profitMargin);
        
        string performanceRating = performanceScore >= 80 ? "Excellent" :
                                   performanceScore >= 60 ? "Good" :
                                   performanceScore >= 40 ? "Average" : "Below Average";
        
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

    private int CalculatePerformanceScore(decimal yieldPerHa, decimal costPerHa, decimal profitMargin)
    {
        // Simplified scoring (would need industry benchmarks)
        int score = 50; // Base score
        
        // Yield component (up to +30)
        if (yieldPerHa >= 7000) score += 30;
        else if (yieldPerHa >= 6000) score += 20;
        else if (yieldPerHa >= 5000) score += 10;
        
        // Profit margin component (up to +20)
        if (profitMargin >= 30) score += 20;
        else if (profitMargin >= 20) score += 15;
        else if (profitMargin >= 10) score += 10;
        
        return Math.Min(100, Math.Max(0, score));
    }

    #endregion

    #region Utilities

    private string? SerializeGeometry(NetTopologySuite.Geometries.Geometry? geometry)
    {
        if (geometry == null) return null;
        try
        {
            var writer = new GeoJsonWriter();
            return writer.Write(geometry);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}

