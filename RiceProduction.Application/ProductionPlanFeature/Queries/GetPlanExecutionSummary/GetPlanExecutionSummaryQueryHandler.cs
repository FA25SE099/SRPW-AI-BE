using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanExecutionSummary;

public class GetPlanExecutionSummaryQueryHandler : IRequestHandler<GetPlanExecutionSummaryQuery, Result<PlanExecutionSummaryResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPlanExecutionSummaryQueryHandler> _logger;

    public GetPlanExecutionSummaryQueryHandler(
        IApplicationDbContext context,
        ILogger<GetPlanExecutionSummaryQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<PlanExecutionSummaryResponse>> Handle(GetPlanExecutionSummaryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _context.ProductionPlans
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.Group)
                    .ThenInclude(g => g!.YearSeason)
                        .ThenInclude(ys => ys!.Season)
                .Include(p => p.Group)
                    .ThenInclude(g => g!.GroupPlots)
                        .ThenInclude(gp => gp.Plot)
                .Include(p => p.Approver)
                .Include(p => p.CurrentProductionStages)
                    .ThenInclude(s => s.ProductionPlanTasks)
                .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

            if (plan == null)
            {
                return Result<PlanExecutionSummaryResponse>.Failure($"Production Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }

            var tasksList = await _context.CultivationTasks
                .AsNoTracking()
                .Include(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt.ProductionStage)
                .Include(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.Farmer)
                .Where(ct => ct.ProductionPlanTask.ProductionStage.ProductionPlanId == request.PlanId)
                .ToListAsync(cancellationToken);

            var totalTasks = tasksList.Count;
            var completedTasks = tasksList.Count(t => t.Status == TaskStatus.Completed);
            var inProgressTasks = tasksList.Count(t => t.Status == TaskStatus.InProgress);
            var pendingTasks = tasksList.Count(t => t.Status == TaskStatus.Draft || t.Status == TaskStatus.PendingApproval);

            var completionPercentage = totalTasks > 0 ? (decimal)completedTasks / totalTasks * 100 : 0;

            var estimatedCost = plan.CurrentProductionStages
                .SelectMany(s => s.ProductionPlanTasks)
                .Sum(t => t.EstimatedMaterialCost);

            var actualCost = tasksList.Sum(t => t.ActualMaterialCost + t.ActualServiceCost);

            var firstTaskStarted = tasksList
                .Where(t => t.ActualStartDate.HasValue)
                .OrderBy(t => t.ActualStartDate)
                .FirstOrDefault()?.ActualStartDate;

            var lastTaskCompleted = tasksList
                .Where(t => t.ActualEndDate.HasValue)
                .OrderByDescending(t => t.ActualEndDate)
                .FirstOrDefault()?.ActualEndDate;

            var plotSummaries = tasksList
                .GroupBy(t => t.PlotCultivation.PlotId)
                .Select(g =>
                {
                    var plot = g.First().PlotCultivation.Plot;
                    var taskCount = g.Count();
                    var plotCompletedTasks = g.Count(t => t.Status == TaskStatus.Completed);

                    return new PlotExecutionSummary
                    {
                        PlotId = g.Key,
                        PlotName = $"{plot.SoThua}/{plot.SoTo}",
                        FarmerName = plot.Farmer?.FullName ?? "Unknown",
                        PlotArea = plot.Area,
                        TaskCount = taskCount,
                        CompletedTasks = plotCompletedTasks,
                        CompletionRate = taskCount > 0 ? (decimal)plotCompletedTasks / taskCount * 100 : 0
                    };
                })
                .OrderBy(ps => ps.PlotName)
                .ToList();

            var response = new PlanExecutionSummaryResponse
            {
                PlanId = plan.Id,
                PlanName = plan.PlanName,
                ApprovedAt = plan.ApprovedAt ?? DateTime.MinValue,
                ApprovedByExpert = plan.Approver?.FullName ?? "Unknown",
                
                GroupId = plan.GroupId ?? Guid.Empty,
                GroupName = plan.Group?.GroupName ?? "Unknown",
                SeasonName = plan.Group?.YearSeason?.Season?.SeasonName ?? "Unknown",
                TotalArea = plan.TotalArea ?? 0,
                PlotCount = plan.Group?.GroupPlots?.Count ?? 0,
                FarmerCount = plan.Group?.GroupPlots?.Select(gp => gp.Plot.FarmerId).Distinct().Count() ?? 0,
                
                TotalTasksCreated = totalTasks,
                TasksCompleted = completedTasks,
                TasksInProgress = inProgressTasks,
                TasksPending = pendingTasks,
                CompletionPercentage = completionPercentage,
                
                EstimatedCost = estimatedCost,
                ActualCost = actualCost,
                
                FirstTaskStarted = firstTaskStarted,
                LastTaskCompleted = lastTaskCompleted,
                
                PlotSummaries = plotSummaries
            };

            _logger.LogInformation("Successfully retrieved execution summary for plan {PlanId}", request.PlanId);

            return Result<PlanExecutionSummaryResponse>.Success(response, "Successfully retrieved plan execution summary.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution summary for plan {PlanId}", request.PlanId);
            return Result<PlanExecutionSummaryResponse>.Failure("An error occurred while retrieving plan execution summary.", "GetExecutionSummaryFailed");
        }
    }
}

