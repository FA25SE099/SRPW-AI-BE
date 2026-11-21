using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonDashboard;

public class GetYearSeasonDashboardQueryHandler 
    : IRequestHandler<GetYearSeasonDashboardQuery, Result<YearSeasonDashboardDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetYearSeasonDashboardQueryHandler> _logger;

    public GetYearSeasonDashboardQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetYearSeasonDashboardQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<YearSeasonDashboardDto>> Handle(
        GetYearSeasonDashboardQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load YearSeason with all necessary relationships
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .AsNoTracking()
                .Include(ys => ys.Season)
                .Include(ys => ys.Cluster)
                .Include(ys => ys.RiceVariety)
                .Include(ys => ys.ManagedByExpert)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return Result<YearSeasonDashboardDto>.Failure($"YearSeason with ID {request.YearSeasonId} not found");
            }

            var dashboard = new YearSeasonDashboardDto();
            var now = DateTime.UtcNow;

            dashboard.Season = BuildSeasonInfo(yearSeason);

            var groups = await _unitOfWork.Repository<Group>()
                .GetQueryable()
                .AsNoTracking()
                .Where(g => g.YearSeasonId == yearSeason.Id)
                .Select(g => new GroupDashboardItem(
                    g.Id,
                    g.Status,
                    g.SupervisorId,
                    g.TotalArea,
                    g.GroupPlots.Select(gp => gp.PlotId).ToList()))
                .ToListAsync(cancellationToken);

            var plotIds = groups
                .SelectMany(g => g.PlotIds)
                .Distinct()
                .ToList();

            dashboard.GroupStatus = await BuildGroupFormationStatus(groups, plotIds, cancellationToken);

            var groupIds = groups.Select(g => g.Id).ToList();
            var productionPlans = groupIds.Count > 0
                ? await _unitOfWork.Repository<ProductionPlan>()
                    .GetQueryable()
                    .AsNoTracking()
                    .Where(pp => pp.GroupId.HasValue && groupIds.Contains(pp.GroupId.Value))
                    .ToListAsync(cancellationToken)
                : new List<ProductionPlan>();

            dashboard.PlanningStatus = BuildProductionPlanningStatus(groups.Count, productionPlans);

            var plotCultivationIds = plotIds.Count > 0
                ? await _unitOfWork.Repository<PlotCultivation>()
                    .GetQueryable()
                    .AsNoTracking()
                    .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == yearSeason.SeasonId)
                    .Select(pc => pc.Id)
                    .ToListAsync(cancellationToken)
                : new List<Guid>();

            var materialDistributions = plotCultivationIds.Count > 0
                ? await _unitOfWork.Repository<MaterialDistribution>()
                    .GetQueryable()
                    .AsNoTracking()
                    .Where(md => plotCultivationIds.Contains(md.PlotCultivationId))
                    .ToListAsync(cancellationToken)
                : new List<MaterialDistribution>();

            dashboard.MaterialStatus = BuildMaterialDistributionStatus(materialDistributions, now);
            dashboard.Timeline = BuildTimeline(yearSeason, now);
            dashboard.Alerts = GenerateAlerts(yearSeason, dashboard, now);

            _logger.LogInformation(
                "Generated dashboard for YearSeason {YearSeasonId}: {GroupCount} groups, {PlanCount} plans, {DistributionCount} distributions",
                yearSeason.Id, dashboard.GroupStatus.TotalGroups, dashboard.PlanningStatus.TotalPlans, dashboard.MaterialStatus.TotalDistributions);

            return Result<YearSeasonDashboardDto>.Success(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dashboard for YearSeason {YearSeasonId}", request.YearSeasonId);
            return Result<YearSeasonDashboardDto>.Failure($"Error generating dashboard: {ex.Message}");
        }
    }

    private static YearSeasonInfo BuildSeasonInfo(YearSeason yearSeason) => new()
    {
        YearSeasonId = yearSeason.Id,
        SeasonName = yearSeason.Season?.SeasonName ?? "Unknown",
        SeasonType = yearSeason.Season?.SeasonType ?? "",
        Year = yearSeason.Year,
        Status = yearSeason.Status.ToString(),
        StartDate = yearSeason.StartDate,
        EndDate = yearSeason.EndDate,
        RiceVarietyName = yearSeason.RiceVariety?.VarietyName ?? "Unknown",
        ClusterName = yearSeason.Cluster?.ClusterName ?? "Unknown",
        ExpertName = yearSeason.ManagedByExpert?.FullName,
        AllowedPlantingFlexibilityDays = yearSeason.AllowedPlantingFlexibilityDays,
        MaterialConfirmationDaysBeforePlanting = yearSeason.MaterialConfirmationDaysBeforePlanting
    };

    private sealed record GroupDashboardItem(
        Guid Id,
        GroupStatus Status,
        Guid? SupervisorId,
        decimal? TotalArea,
        List<Guid> PlotIds);

    private async Task<GroupFormationStatus> BuildGroupFormationStatus(
        List<GroupDashboardItem> groups,
        List<Guid> plotIds,
        CancellationToken cancellationToken)
    {
        var status = new GroupFormationStatus
        {
            TotalGroups = groups.Count,
            ActiveGroups = groups.Count(g => g.Status == GroupStatus.Active),
            DraftGroups = groups.Count(g => g.Status == GroupStatus.Draft),
            CompletedGroups = groups.Count(g => g.Status == GroupStatus.Completed),
            GroupsWithSupervisor = groups.Count(g => g.SupervisorId.HasValue),
            GroupsWithoutSupervisor = groups.Count(g => !g.SupervisorId.HasValue),
            TotalAreaHectares = groups.Sum(g => g.TotalArea ?? 0),
            TotalPlotsInGroups = groups.Sum(g => g.PlotIds.Count)
        };

        if (plotIds.Any())
        {
            status.TotalFarmersInGroups = await _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .AsNoTracking()
                .Where(p => plotIds.Contains(p.Id))
                .Select(p => p.FarmerId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        return status;
    }

    private static ProductionPlanningStatus BuildProductionPlanningStatus(int totalGroups, List<ProductionPlan> plans)
    {
        var groupsWithPlans = plans.Where(p => p.GroupId.HasValue).Select(p => p.GroupId!.Value).Distinct().Count();

        return new ProductionPlanningStatus
        {
            TotalPlans = plans.Count,
            PlansDraft = plans.Count(p => p.Status == TaskStatus.Draft),
            PlansPendingApproval = plans.Count(p => p.Status == TaskStatus.PendingApproval),
            PlansApproved = plans.Count(p => p.Status == TaskStatus.Approved),
            PlansCancelled = plans.Count(p => p.Status == TaskStatus.Cancelled),
            GroupsWithPlans = groupsWithPlans,
            GroupsWithoutPlans = totalGroups - groupsWithPlans,
            PlanningCompletionRate = totalGroups > 0 ? (decimal)groupsWithPlans / totalGroups * 100 : 0,
            EarliestPlanSubmission = plans.Where(p => p.SubmittedAt.HasValue).Min(p => p.SubmittedAt),
            LatestPlanApproval = plans.Where(p => p.ApprovedAt.HasValue).Max(p => p.ApprovedAt)
        };
    }

    private static MaterialDistributionStatus BuildMaterialDistributionStatus(List<MaterialDistribution> distributions, DateTime now)
    {
        var completedCount = distributions.Count(d => d.Status == DistributionStatus.Completed);
        var totalCount = distributions.Count;

        return new MaterialDistributionStatus
        {
            TotalDistributions = totalCount,
            DistributionsPending = distributions.Count(d => d.Status == DistributionStatus.Pending),
            DistributionsPartiallyConfirmed = distributions.Count(d => d.Status == DistributionStatus.PartiallyConfirmed),
            DistributionsCompleted = completedCount,
            DistributionsRejected = distributions.Count(d => d.Status == DistributionStatus.Rejected),
            DistributionsOverdue = distributions.Count(d =>
                d.Status == DistributionStatus.Pending &&
                d.DistributionDeadline < now),
            MaterialCompletionRate = totalCount > 0 ? (decimal)completedCount / totalCount * 100 : 0,
            UniqueMaterialsDistributed = distributions.Select(d => d.MaterialId).Distinct().Count(),
            PlotsReceivingMaterials = distributions.Select(d => d.PlotCultivationId).Distinct().Count()
        };
    }

    private static YearSeasonTimeline BuildTimeline(YearSeason yearSeason, DateTime now)
    {
        var totalDays = (yearSeason.EndDate - yearSeason.StartDate).Days;
        var daysElapsed = Math.Max(0, (now - yearSeason.StartDate).Days);
        var daysRemaining = Math.Max(0, (yearSeason.EndDate - now).Days);

        int DaysUntil(DateTime? target) =>
            target.HasValue ? Math.Max(0, (int)(target.Value - now).TotalDays) : 0;

        return new YearSeasonTimeline
        {
            PlanningWindowStart = yearSeason.PlanningWindowStart,
            PlanningWindowEnd = yearSeason.PlanningWindowEnd,
            SeasonStartDate = yearSeason.StartDate,
            SeasonEndDate = yearSeason.EndDate,
            BreakStartDate = yearSeason.BreakStartDate,
            BreakEndDate = yearSeason.BreakEndDate,

            DaysUntilPlanningWindowStart = DaysUntil(yearSeason.PlanningWindowStart),
            DaysUntilPlanningWindowEnd = DaysUntil(yearSeason.PlanningWindowEnd),
            DaysUntilSeasonStart = DaysUntil(yearSeason.StartDate),
            DaysUntilSeasonEnd = DaysUntil(yearSeason.EndDate),
            TotalSeasonDays = totalDays,
            DaysElapsed = daysElapsed,
            DaysRemaining = daysRemaining,
            ProgressPercentage = totalDays > 0 ? Math.Min(100, (decimal)daysElapsed / totalDays * 100) : 0,

            IsPlanningWindowOpen = (!yearSeason.PlanningWindowStart.HasValue || now >= yearSeason.PlanningWindowStart.Value) &&
                                   (!yearSeason.PlanningWindowEnd.HasValue || now <= yearSeason.PlanningWindowEnd.Value),
            HasSeasonStarted = now >= yearSeason.StartDate,
            HasSeasonEnded = now >= yearSeason.EndDate
        };
    }

    private static List<YearSeasonAlert> GenerateAlerts(YearSeason yearSeason, YearSeasonDashboardDto dashboard, DateTime now)
    {
        var alerts = new List<YearSeasonAlert>();

        void AddAlert(string type, string code, string message, string category, Dictionary<string, object>? metadata = null)
        {
            var alert = new YearSeasonAlert
            {
                Type = type,
                Code = code,
                Message = message,
                Category = category,
                Timestamp = now
            };

            if (metadata != null)
            {
                alert.Metadata = metadata;
            }

            alerts.Add(alert);
        }

        if (yearSeason.Status == SeasonStatus.Draft)
        {
            AddAlert("Info", "SEASON_DRAFT",
                "YearSeason is in Draft status. Planning window has not opened yet.", "Timeline");
        }

        if (dashboard.Timeline.IsPlanningWindowOpen &&
            dashboard.Timeline.DaysUntilPlanningWindowEnd is > 0 and <= 3)
        {
            AddAlert("Warning", "PLANNING_WINDOW_CLOSING",
                $"Planning window closes in {dashboard.Timeline.DaysUntilPlanningWindowEnd} days", "Planning",
                new Dictionary<string, object>
                {
                    ["deadline"] = yearSeason.PlanningWindowEnd!.Value,
                    ["daysRemaining"] = dashboard.Timeline.DaysUntilPlanningWindowEnd
                });
        }

        if (dashboard.GroupStatus.TotalGroups == 0)
        {
            AddAlert("Warning", "NO_GROUPS_FORMED",
                "No groups have been formed for this YearSeason yet", "Groups");
        }

        if (dashboard.GroupStatus.GroupsWithoutSupervisor > 0)
        {
            AddAlert("Warning", "GROUPS_WITHOUT_SUPERVISOR",
                $"{dashboard.GroupStatus.GroupsWithoutSupervisor} group(s) do not have an assigned supervisor", "Groups",
                new Dictionary<string, object> { ["count"] = dashboard.GroupStatus.GroupsWithoutSupervisor });
        }

        if (dashboard.PlanningStatus.GroupsWithoutPlans > 0 && dashboard.Timeline.IsPlanningWindowOpen)
        {
            AddAlert("Info", "GROUPS_WITHOUT_PLANS",
                $"{dashboard.PlanningStatus.GroupsWithoutPlans} group(s) have not submitted production plans yet", "Planning",
                new Dictionary<string, object>
                {
                    ["count"] = dashboard.PlanningStatus.GroupsWithoutPlans,
                    ["completionRate"] = dashboard.PlanningStatus.PlanningCompletionRate
                });
        }

        if (dashboard.PlanningStatus.PlansPendingApproval > 0)
        {
            AddAlert("Info", "PLANS_PENDING_APPROVAL",
                $"{dashboard.PlanningStatus.PlansPendingApproval} production plan(s) are waiting for expert approval", "Planning",
                new Dictionary<string, object> { ["count"] = dashboard.PlanningStatus.PlansPendingApproval });
        }

        if (dashboard.MaterialStatus.DistributionsOverdue > 0)
        {
            AddAlert("Error", "DISTRIBUTIONS_OVERDUE",
                $"{dashboard.MaterialStatus.DistributionsOverdue} material distribution(s) are overdue", "Materials",
                new Dictionary<string, object> { ["count"] = dashboard.MaterialStatus.DistributionsOverdue });
        }

        if (dashboard.MaterialStatus.TotalDistributions > 0 && dashboard.MaterialStatus.MaterialCompletionRate == 100)
        {
            AddAlert("Success", "ALL_MATERIALS_DISTRIBUTED",
                "All materials have been successfully distributed and confirmed", "Materials");
        }

        if (dashboard.Timeline.HasSeasonStarted && !dashboard.Timeline.HasSeasonEnded && dashboard.Timeline.DaysRemaining <= 7)
        {
            AddAlert("Warning", "SEASON_ENDING_SOON",
                $"Season ends in {dashboard.Timeline.DaysRemaining} days", "Timeline",
                new Dictionary<string, object>
                {
                    ["endDate"] = yearSeason.EndDate,
                    ["daysRemaining"] = dashboard.Timeline.DaysRemaining
                });
        }

        if (dashboard.Timeline.HasSeasonEnded && yearSeason.Status != SeasonStatus.Completed)
        {
            AddAlert("Warning", "SEASON_ENDED_NOT_COMPLETED",
                "Season end date has passed but status is not marked as Completed", "Timeline");
        }

        if (dashboard.PlanningStatus.PlanningCompletionRate == 100 && dashboard.PlanningStatus.TotalPlans > 0)
        {
            AddAlert("Success", "ALL_GROUPS_HAVE_PLANS",
                "All groups have submitted and approved production plans", "Planning");
        }

        return alerts
            .OrderByDescending(a => GetAlertSeverityOrder(a.Type))
            .ThenBy(a => a.Category)
            .ToList();
    }

    private static int GetAlertSeverityOrder(string alertType) => alertType switch
    {
        "Error" => 3,
        "Warning" => 2,
        "Info" => 1,
        _ => 0
    };
}

