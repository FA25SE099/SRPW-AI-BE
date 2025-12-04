using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCurrentPlotCultivation;

public class GetCurrentPlotCultivationQueryHandler : IRequestHandler<GetCurrentPlotCultivationQuery, Result<CurrentPlotCultivationDetailResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetCurrentPlotCultivationQueryHandler> _logger;

    public GetCurrentPlotCultivationQueryHandler(IUnitOfWork unitOfWork, ILogger<GetCurrentPlotCultivationQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CurrentPlotCultivationDetailResponse>> Handle(GetCurrentPlotCultivationQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Load plot with current cultivation
            var plot = await _unitOfWork.Repository<Plot>().FindAsync(
                match: p => p.Id == request.PlotId,
                includeProperties: q => q
                    .Include(p => p.PlotCultivations
                        .Where(pc => pc.Status == CultivationStatus.InProgress || pc.Status == CultivationStatus.Planned))
                        .ThenInclude(pc => pc.Season)
                    .Include(p => p.PlotCultivations)
                        .ThenInclude(pc => pc.RiceVariety)
                    .Include(p => p.PlotCultivations)
                        .ThenInclude(pc => pc.CultivationVersions.Where(v => v.IsActive))
            );

            if (plot == null)
            {
                return Result<CurrentPlotCultivationDetailResponse>.Failure($"Plot with ID {request.PlotId} not found.", "PlotNotFound");
            }

            // Get current active cultivation (InProgress or latest Planned)
            var currentCultivation = plot.PlotCultivations
                .OrderByDescending(pc => pc.Status == CultivationStatus.InProgress)
                .ThenByDescending(pc => pc.PlantingDate)
                .FirstOrDefault();

            if (currentCultivation == null)
            {
                return Result<CurrentPlotCultivationDetailResponse>.Failure($"No active cultivation found for plot {request.PlotId}.", "NoCultivationFound");
            }

            var activeVersion = currentCultivation.CultivationVersions.FirstOrDefault();

            // Load cultivation tasks with related data
            Guid? versionId = activeVersion?.Id;
            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.PlotCultivationId == currentCultivation.Id && ct.VersionId == versionId,
                orderBy: q => q.OrderBy(ct => ct.ExecutionOrder),
#pragma warning disable CS8602 // Dereference of a possibly null reference
                includeProperties: q => q
                    .Include(ct => ct.ProductionPlanTask)!
                        .ThenInclude(ppt => ppt.ProductionStage)!
                        .ThenInclude(ps => ps.ProductionPlan)
                    .Include(ct => ct.CultivationTaskMaterials)
                        .ThenInclude(ctm => ctm.Material)
#pragma warning restore CS8602 // Dereference of a possibly null reference
            );

            // Get production plan info from first task
            var firstTask = tasks.FirstOrDefault();
            var productionPlan = firstTask?.ProductionPlanTask?.ProductionStage?.ProductionPlan;

            // Map tasks to summary
            var taskSummaries = tasks.Select(task => new CultivationTaskSummary
            {
                TaskId = task.Id,
                TaskName = task.CultivationTaskName ?? string.Empty,
                TaskDescription = task.Description ?? string.Empty,
                TaskType = task.TaskType ?? Domain.Enums.TaskType.Sowing,
                Status = task.Status ?? Domain.Enums.TaskStatus.Draft,
                Priority = Domain.Enums.TaskPriority.Normal,
                PlannedStartDate = null,
                PlannedEndDate = task.ScheduledEndDate,
                ActualStartDate = task.ActualStartDate,
                ActualEndDate = task.ActualEndDate,
                OrderIndex = task.ExecutionOrder ?? 0,
                StageName = task.ProductionPlanTask?.ProductionStage?.StageName ?? "N/A",
                Materials = task.CultivationTaskMaterials.Select(ctm => new TaskMaterialSummary
                {
                    MaterialId = ctm.MaterialId,
                    MaterialName = ctm.Material.Name,
                    PlannedQuantity = 0,
                    ActualQuantity = ctm.ActualQuantity,
                    Unit = ctm.Material.Unit
                }).ToList()
            }).ToList();

            // Calculate progress
            var totalTasks = tasks.Count;
            var completedTasks = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Completed);
            var inProgressTasks = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.InProgress);
            var pendingTasks = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Draft || t.Status == Domain.Enums.TaskStatus.PendingApproval);
            var completionPercentage = totalTasks > 0 ? (decimal)completedTasks / totalTasks * 100 : 0;
            var daysElapsed = (DateTime.UtcNow - currentCultivation.PlantingDate).Days;
            
            // Estimate remaining days based on last task scheduled end date
            int? estimatedDaysRemaining = null;
            var lastTask = tasks.OrderByDescending(t => t.ScheduledEndDate).FirstOrDefault();
            if (lastTask?.ScheduledEndDate != null)
            {
                estimatedDaysRemaining = (lastTask.ScheduledEndDate.Value - DateTime.UtcNow).Days;
                if (estimatedDaysRemaining < 0) estimatedDaysRemaining = 0;
            }

            var response = new CurrentPlotCultivationDetailResponse
            {
                PlotCultivationId = currentCultivation.Id,
                PlotId = plot.Id,
                PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                PlotArea = plot.Area,
                
                SeasonId = currentCultivation.SeasonId,
                SeasonName = currentCultivation.Season.SeasonName,
                SeasonStartDate = DateTime.Parse(currentCultivation.Season.StartDate),
                SeasonEndDate = DateTime.Parse(currentCultivation.Season.EndDate),
                
                RiceVarietyId = currentCultivation.RiceVarietyId,
                RiceVarietyName = currentCultivation.RiceVariety.VarietyName,
                RiceVarietyDescription = currentCultivation.RiceVariety.Description,
                
                PlantingDate = currentCultivation.PlantingDate,
                ExpectedYield = currentCultivation.ExpectedYield,
                ActualYield = currentCultivation.ActualYield,
                CultivationArea = currentCultivation.Area,
                Status = currentCultivation.Status,
                
                ProductionPlanId = productionPlan?.Id,
                ProductionPlanName = productionPlan?.PlanName,
                ProductionPlanDescription = null, // ProductionPlan doesn't have Description field
                
                ActiveVersionId = activeVersion?.Id,
                ActiveVersionName = activeVersion?.VersionName,
                
                Tasks = taskSummaries,
                Progress = new CultivationProgress
                {
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    InProgressTasks = inProgressTasks,
                    PendingTasks = pendingTasks,
                    CompletionPercentage = Math.Round(completionPercentage, 2),
                    DaysElapsed = daysElapsed,
                    EstimatedDaysRemaining = estimatedDaysRemaining
                }
            };

            _logger.LogInformation("Successfully retrieved current cultivation plan for plot {PlotId}", request.PlotId);
            return Result<CurrentPlotCultivationDetailResponse>.Success(response, "Successfully retrieved current cultivation plan.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current cultivation plan for plot {PlotId}", request.PlotId);
            return Result<CurrentPlotCultivationDetailResponse>.Failure("An error occurred while retrieving cultivation plan.", "RetrievalFailed");
        }
    }
}
