using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetCurrentPlotCultivation;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetPlotCultivationByGroupAndPlot;

public class GetPlotCultivationByGroupAndPlotQueryHandler : IRequestHandler<GetPlotCultivationByGroupAndPlotQuery, Result<CurrentPlotCultivationDetailResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlotCultivationByGroupAndPlotQueryHandler> _logger;

    public GetPlotCultivationByGroupAndPlotQueryHandler(IUnitOfWork unitOfWork, ILogger<GetPlotCultivationByGroupAndPlotQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CurrentPlotCultivationDetailResponse>> Handle(GetPlotCultivationByGroupAndPlotQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Verify that the plot belongs to the group using GroupPlot
            var groupPlot = await _unitOfWork.Repository<GroupPlot>().FindAsync(
                match: gp => gp.PlotId == request.PlotId && gp.GroupId == request.GroupId
            );

            if (groupPlot == null)
            {
                return Result<CurrentPlotCultivationDetailResponse>.Failure(
                    $"Plot with ID {request.PlotId} does not belong to Group with ID {request.GroupId}.",
                    "PlotNotInGroup");
            }

            // 2. Load the Group to get the SeasonId
            var group = await _unitOfWork.Repository<Group>().FindAsync(
                match: g => g.Id == request.GroupId
            );

            if (group == null)
            {
                return Result<CurrentPlotCultivationDetailResponse>.Failure(
                    $"Group with ID {request.GroupId} not found.",
                    "GroupNotFound");
            }

            if (group.YearSeason?.SeasonId == null)
            {
                return Result<CurrentPlotCultivationDetailResponse>.Failure(
                    $"Group with ID {request.GroupId} does not have a season assigned.",
                    "GroupSeasonNotAssigned");
            }

            // 3. Find PlotCultivation using PlotId and SeasonId
            var plotCultivation = await _unitOfWork.Repository<PlotCultivation>().FindAsync(
                match: pc => pc.PlotId == request.PlotId && pc.SeasonId == group.YearSeason.SeasonId,
                includeProperties: q => q
                    .Include(pc => pc.Plot)
                    .Include(pc => pc.Season)
                    .Include(pc => pc.RiceVariety)
                    .Include(pc => pc.CultivationVersions)
            );

            if (plotCultivation == null)
            {
                return Result<CurrentPlotCultivationDetailResponse>.Failure(
                    $"No cultivation found for Plot {request.PlotId} in Season {group.YearSeason.SeasonId}.",
                    "PlotCultivationNotFound");
            }

            // Get the latest version (highest VersionOrder)
            var latestVersion = plotCultivation.CultivationVersions
                .OrderByDescending(v => v.VersionOrder)
                .FirstOrDefault();

            // 4. Load cultivation tasks with related data for the latest version
            Guid? versionId = latestVersion?.Id;
            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.PlotCultivationId == plotCultivation.Id && ct.VersionId == versionId,
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

            // Log warning if duplicates are detected
            var duplicateCount = tasks.Count - tasks.DistinctBy(t => t.Id).Count();
            if (duplicateCount > 0)
            {
                _logger.LogWarning(
                    "Found {DuplicateCount} duplicate tasks for PlotCultivation {PlotCultivationId} Version {VersionId}. " +
                    "This indicates a data integrity issue that should be investigated.",
                    duplicateCount, plotCultivation.Id, versionId);
            }

            // Deduplicate tasks by keeping only unique task IDs (taking the first occurrence)
            var uniqueTasks = tasks
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            // 5. Get production plan info from first task
            var firstTask = uniqueTasks.FirstOrDefault();
            var productionPlan = firstTask?.ProductionPlanTask?.ProductionStage?.ProductionPlan;

            // 6. Group tasks by stage and create nested structure
            var stagesGroup = uniqueTasks
                .GroupBy(task => new
                {
                    StageId = task.ProductionPlanTask?.ProductionStage?.Id,
                    StageName = task.ProductionPlanTask?.ProductionStage?.StageName ?? "N/A",
                    SequenceOrder = task.ProductionPlanTask?.ProductionStage?.SequenceOrder ?? int.MaxValue,
                    Description = task.ProductionPlanTask?.ProductionStage?.Description,
                    TypicalDurationDays = task.ProductionPlanTask?.ProductionStage?.TypicalDurationDays
                })
                .OrderBy(g => g.Key.SequenceOrder)
                .Select(stageGroup => new CultivationStageSummary
                {
                    StageId = stageGroup.Key.StageId,
                    StageName = stageGroup.Key.StageName,
                    SequenceOrder = stageGroup.Key.SequenceOrder,
                    Description = stageGroup.Key.Description,
                    TypicalDurationDays = stageGroup.Key.TypicalDurationDays,
                    Tasks = stageGroup
                        .OrderBy(task => task.ExecutionOrder ?? 0)
                        .Select(task => new CultivationTaskSummary
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
                            Materials = task.CultivationTaskMaterials.Select(ctm => new TaskMaterialSummary
                            {
                                MaterialId = ctm.MaterialId,
                                MaterialName = ctm.Material.Name,
                                PlannedQuantity = 0,
                                ActualQuantity = ctm.ActualQuantity,
                                Unit = ctm.Material.Unit
                            }).ToList()
                        }).ToList()
                })
                .ToList();

            // 7. Calculate progress (using unique tasks)
            var totalTasks = uniqueTasks.Count;
            var completedTasks = uniqueTasks.Count(t => t.Status == Domain.Enums.TaskStatus.Completed);
            var inProgressTasks = uniqueTasks.Count(t => t.Status == Domain.Enums.TaskStatus.InProgress);
            var pendingTasks = uniqueTasks.Count(t => t.Status == Domain.Enums.TaskStatus.Draft || t.Status == Domain.Enums.TaskStatus.PendingApproval);
            var completionPercentage = totalTasks > 0 ? (decimal)completedTasks / totalTasks * 100 : 0;
            var daysElapsed = (DateTime.UtcNow - plotCultivation.PlantingDate).Days;

            // Estimate remaining days based on last task scheduled end date
            int? estimatedDaysRemaining = null;
            var lastTask = uniqueTasks.OrderByDescending(t => t.ScheduledEndDate).FirstOrDefault();
            if (lastTask?.ScheduledEndDate != null)
            {
                estimatedDaysRemaining = (lastTask.ScheduledEndDate.Value - DateTime.UtcNow).Days;
                if (estimatedDaysRemaining < 0) estimatedDaysRemaining = 0;
            }

            // 8. Build response
            var response = new CurrentPlotCultivationDetailResponse
            {
                PlotCultivationId = plotCultivation.Id,
                PlotId = plotCultivation.Plot.Id,
                PlotName = $"Thửa {plotCultivation.Plot.SoThua ?? 0}, Tờ {plotCultivation.Plot.SoTo ?? 0}",
                PlotArea = plotCultivation.Plot.Area,

                SeasonId = plotCultivation.SeasonId,
                SeasonName = plotCultivation.Season.SeasonName,
                SeasonStartDate = DateTime.Parse(plotCultivation.Season.StartDate),
                SeasonEndDate = DateTime.Parse(plotCultivation.Season.EndDate),

                RiceVarietyId = plotCultivation.RiceVarietyId,
                RiceVarietyName = plotCultivation.RiceVariety.VarietyName,
                RiceVarietyDescription = plotCultivation.RiceVariety.Description,

                PlantingDate = plotCultivation.PlantingDate,
                ExpectedYield = plotCultivation.ExpectedYield,
                ActualYield = plotCultivation.ActualYield,
                CultivationArea = plotCultivation.Area,
                Status = plotCultivation.Status,

                ProductionPlanId = productionPlan?.Id,
                ProductionPlanName = productionPlan?.PlanName,
                ProductionPlanDescription = null,

                ActiveVersionId = latestVersion?.Id,
                ActiveVersionName = latestVersion?.VersionName,

                Stages = stagesGroup,
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

            _logger.LogInformation(
                "Successfully retrieved cultivation plan for Plot {PlotId} in Group {GroupId} for Season {SeasonId} using latest version {VersionOrder}. " +
                "Total unique tasks: {UniqueCount} (from {TotalCount} records)",
                request.PlotId, request.GroupId, group.YearSeason.SeasonId, latestVersion?.VersionOrder, uniqueTasks.Count, tasks.Count);

            return Result<CurrentPlotCultivationDetailResponse>.Success(
                response,
                "Successfully retrieved cultivation plan for plot in group.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving cultivation plan for Plot {PlotId} in Group {GroupId}",
                request.PlotId, request.GroupId);

            return Result<CurrentPlotCultivationDetailResponse>.Failure(
                "An error occurred while retrieving cultivation plan.",
                "RetrievalFailed");
        }
    }
}
