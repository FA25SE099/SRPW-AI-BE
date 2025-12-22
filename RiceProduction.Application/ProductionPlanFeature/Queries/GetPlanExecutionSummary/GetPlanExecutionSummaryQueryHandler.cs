using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanExecutionSummary;

public class GetPlanExecutionSummaryQueryHandler : IRequestHandler<GetPlanExecutionSummaryQuery, Result<PlanExecutionSummaryResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlanExecutionSummaryQueryHandler> _logger;

    public GetPlanExecutionSummaryQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetPlanExecutionSummaryQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PlanExecutionSummaryResponse>> Handle(GetPlanExecutionSummaryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(
                match: p => p.Id == request.PlanId,
                includeProperties: q => q
                    .Include(p => p.Group)
                        .ThenInclude(g => g.YearSeason)
                            .ThenInclude(ys => ys.Season)
                    .Include(p => p.Group)
                        .ThenInclude(g => g.GroupPlots)
                            .ThenInclude(gp => gp.Plot)
                                .ThenInclude(pl => pl.Farmer)
                    .Include(p => p.Approver)
                    .Include(p => p.CurrentProductionStages)
                        .ThenInclude(s => s.ProductionPlanTasks)
            );

            if (plan == null)
            {
                return Result<PlanExecutionSummaryResponse>.Failure($"Production Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }

            var cultivationTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.ProductionPlanTask.ProductionStage.ProductionPlanId == request.PlanId,
                includeProperties: q => q
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionStage)
                    .Include(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.Plot)
                            .ThenInclude(p => p.Farmer)
            );

            var tasksList = cultivationTasks.ToList();
            
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
                .GroupBy(t => new { t.PlotCultivation.PlotId, t.PlotCultivation.Plot })
                .Select(g => new PlotExecutionSummary
                {
                    PlotId = g.Key.PlotId,
                    PlotName = $"{g.Key.Plot.SoThua}/{g.Key.Plot.SoTo}",
                    FarmerName = g.Key.Plot.Farmer?.FullName ?? "Unknown",
                    PlotArea = g.Key.Plot.Area,
                    TaskCount = g.Count(),
                    CompletedTasks = g.Count(t => t.Status == TaskStatus.Completed),
                    CompletionRate = g.Count() > 0 ? (decimal)g.Count(t => t.Status == TaskStatus.Completed) / g.Count() * 100 : 0
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

