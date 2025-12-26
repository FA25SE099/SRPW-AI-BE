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

            // Build Season Info
            dashboard.Season = new YearSeasonInfo
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

            // Get all groups for this YearSeason
            var groups = await _unitOfWork.Repository<Group>()
                .GetQueryable()
                .Include(g => g.GroupPlots)
                .Where(g => g.YearSeasonId == yearSeason.Id)
                .ToListAsync(cancellationToken);

            // Build Group Formation Status
            dashboard.GroupStatus = await BuildGroupFormationStatus(groups, cancellationToken);

            // Get all production plans for these groups
            var groupIds = groups.Select(g => g.Id).ToList();
            var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
                .GetQueryable()
                .Where(pp => pp.GroupId.HasValue && groupIds.Contains(pp.GroupId.Value))
                .ToListAsync(cancellationToken);

            // Build Production Planning Status
            dashboard.PlanningStatus = BuildProductionPlanningStatus(groups, productionPlans);

            // Get all plot cultivations for this season
            var plotIds = groups
                .SelectMany(g => g.GroupPlots)
                .Select(gp => gp.PlotId)
                .Distinct()
                .ToList();

            var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
                .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == yearSeason.SeasonId)
                .ToListAsync(cancellationToken);

            var plotCultivationIds = plotCultivations.Select(pc => pc.Id).ToList();

            // Get all material distributions
            var materialDistributions = await _unitOfWork.Repository<MaterialDistribution>()
                .GetQueryable()
                .Where(md => plotCultivationIds.Contains(md.PlotCultivationId))
                .ToListAsync(cancellationToken);

            // Build Material Distribution Status
            dashboard.MaterialStatus = BuildMaterialDistributionStatus(materialDistributions, now);

            // Build Timeline
            dashboard.Timeline = BuildTimeline(yearSeason, now);

            // Generate Alerts
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

    private async Task<GroupFormationStatus> BuildGroupFormationStatus(List<Group> groups, CancellationToken cancellationToken)
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
            TotalPlotsInGroups = groups.Sum(g => g.GroupPlots.Count)
        };

        // Get unique farmers
        var plotIds = groups.SelectMany(g => g.GroupPlots).Select(gp => gp.PlotId).Distinct().ToList();
        if (plotIds.Any())
        {
            var farmerIds = await _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .Where(p => plotIds.Contains(p.Id))
                .Select(p => p.FarmerId)
                .Distinct()
                .CountAsync(cancellationToken);
            status.TotalFarmersInGroups = farmerIds;
        }

        return status;
    }

    private ProductionPlanningStatus BuildProductionPlanningStatus(List<Group> groups, List<ProductionPlan> plans)
    {
        var groupsWithPlans = plans.Where(p => p.GroupId.HasValue).Select(p => p.GroupId.Value).Distinct().Count();
        var totalGroups = groups.Count;

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

    private MaterialDistributionStatus BuildMaterialDistributionStatus(List<MaterialDistribution> distributions, DateTime now)
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

    private YearSeasonTimeline BuildTimeline(YearSeason yearSeason, DateTime now)
    {
        var totalDays = (yearSeason.EndDate - yearSeason.StartDate).Days;
        var daysElapsed = Math.Max(0, (now - yearSeason.StartDate).Days);
        var daysRemaining = Math.Max(0, (yearSeason.EndDate - now).Days);

        return new YearSeasonTimeline
        {
            PlanningWindowStart = yearSeason.PlanningWindowStart,
            PlanningWindowEnd = yearSeason.PlanningWindowEnd,
            SeasonStartDate = yearSeason.StartDate,
            SeasonEndDate = yearSeason.EndDate,
            BreakStartDate = yearSeason.BreakStartDate,
            BreakEndDate = yearSeason.BreakEndDate,
            
            DaysUntilPlanningWindowStart = yearSeason.PlanningWindowStart.HasValue 
                ? Math.Max(0, (int)(yearSeason.PlanningWindowStart.Value - now).TotalDays) 
                : 0,
            DaysUntilPlanningWindowEnd = yearSeason.PlanningWindowEnd.HasValue 
                ? Math.Max(0, (int)(yearSeason.PlanningWindowEnd.Value - now).TotalDays) 
                : 0,
            DaysUntilSeasonStart = Math.Max(0, (int)(yearSeason.StartDate - now).TotalDays),
            DaysUntilSeasonEnd = Math.Max(0, (int)(yearSeason.EndDate - now).TotalDays),
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

    private List<YearSeasonAlert> GenerateAlerts(YearSeason yearSeason, YearSeasonDashboardDto dashboard, DateTime now)
    {
        var alerts = new List<YearSeasonAlert>();

        // Planning Window Alerts
        if (yearSeason.Status == SeasonStatus.Draft)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Info",
                Code = "SEASON_DRAFT",
                Message = "YearSeason is in Draft status. Planning window has not opened yet.",
                Category = "Timeline",
                Timestamp = now
            });
        }

        if (dashboard.Timeline.IsPlanningWindowOpen && dashboard.Timeline.DaysUntilPlanningWindowEnd <= 3 && dashboard.Timeline.DaysUntilPlanningWindowEnd > 0)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Warning",
                Code = "PLANNING_WINDOW_CLOSING",
                Message = $"Planning window closes in {dashboard.Timeline.DaysUntilPlanningWindowEnd} days",
                Category = "Planning",
                Timestamp = now,
                Metadata = new Dictionary<string, object>
                {
                    ["deadline"] = yearSeason.PlanningWindowEnd!.Value,
                    ["daysRemaining"] = dashboard.Timeline.DaysUntilPlanningWindowEnd
                }
            });
        }

        // Group Formation Alerts
        if (dashboard.GroupStatus.TotalGroups == 0)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Warning",
                Code = "NO_GROUPS_FORMED",
                Message = "No groups have been formed for this YearSeason yet",
                Category = "Groups",
                Timestamp = now
            });
        }

        if (dashboard.GroupStatus.GroupsWithoutSupervisor > 0)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Warning",
                Code = "GROUPS_WITHOUT_SUPERVISOR",
                Message = $"{dashboard.GroupStatus.GroupsWithoutSupervisor} group(s) do not have an assigned supervisor",
                Category = "Groups",
                Timestamp = now,
                Metadata = new Dictionary<string, object>
                {
                    ["count"] = dashboard.GroupStatus.GroupsWithoutSupervisor
                }
            });
        }

        // Production Planning Alerts
        if (dashboard.PlanningStatus.GroupsWithoutPlans > 0 && dashboard.Timeline.IsPlanningWindowOpen)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Info",
                Code = "GROUPS_WITHOUT_PLANS",
                Message = $"{dashboard.PlanningStatus.GroupsWithoutPlans} group(s) have not submitted production plans yet",
                Category = "Planning",
                Timestamp = now,
                Metadata = new Dictionary<string, object>
                {
                    ["count"] = dashboard.PlanningStatus.GroupsWithoutPlans,
                    ["completionRate"] = dashboard.PlanningStatus.PlanningCompletionRate
                }
            });
        }

        if (dashboard.PlanningStatus.PlansPendingApproval > 0)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Info",
                Code = "PLANS_PENDING_APPROVAL",
                Message = $"{dashboard.PlanningStatus.PlansPendingApproval} production plan(s) are waiting for expert approval",
                Category = "Planning",
                Timestamp = now,
                Metadata = new Dictionary<string, object>
                {
                    ["count"] = dashboard.PlanningStatus.PlansPendingApproval
                }
            });
        }

        // Material Distribution Alerts
        if (dashboard.MaterialStatus.DistributionsOverdue > 0)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Error",
                Code = "DISTRIBUTIONS_OVERDUE",
                Message = $"{dashboard.MaterialStatus.DistributionsOverdue} material distribution(s) are overdue",
                Category = "Materials",
                Timestamp = now,
                Metadata = new Dictionary<string, object>
                {
                    ["count"] = dashboard.MaterialStatus.DistributionsOverdue
                }
            });
        }

        if (dashboard.MaterialStatus.TotalDistributions > 0 && dashboard.MaterialStatus.MaterialCompletionRate == 100)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Success",
                Code = "ALL_MATERIALS_DISTRIBUTED",
                Message = "All materials have been successfully distributed and confirmed",
                Category = "Materials",
                Timestamp = now
            });
        }

        // Timeline Alerts
        if (dashboard.Timeline.HasSeasonStarted && !dashboard.Timeline.HasSeasonEnded)
        {
            if (dashboard.Timeline.DaysRemaining <= 7)
            {
                alerts.Add(new YearSeasonAlert
                {
                    Type = "Warning",
                    Code = "SEASON_ENDING_SOON",
                    Message = $"Season ends in {dashboard.Timeline.DaysRemaining} days",
                    Category = "Timeline",
                    Timestamp = now,
                    Metadata = new Dictionary<string, object>
                    {
                        ["endDate"] = yearSeason.EndDate,
                        ["daysRemaining"] = dashboard.Timeline.DaysRemaining
                    }
                });
            }
        }

        if (dashboard.Timeline.HasSeasonEnded && yearSeason.Status != SeasonStatus.Completed)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Warning",
                Code = "SEASON_ENDED_NOT_COMPLETED",
                Message = "Season end date has passed but status is not marked as Completed",
                Category = "Timeline",
                Timestamp = now
            });
        }

        // Success Alerts
        if (dashboard.PlanningStatus.PlanningCompletionRate == 100 && dashboard.PlanningStatus.TotalPlans > 0)
        {
            alerts.Add(new YearSeasonAlert
            {
                Type = "Success",
                Code = "ALL_GROUPS_HAVE_PLANS",
                Message = "All groups have submitted and approved production plans",
                Category = "Planning",
                Timestamp = now
            });
        }

        return alerts.OrderByDescending(a => a.Type == "Error" ? 3 : a.Type == "Warning" ? 2 : a.Type == "Info" ? 1 : 0)
                    .ThenBy(a => a.Category)
                    .ToList();
    }
}

