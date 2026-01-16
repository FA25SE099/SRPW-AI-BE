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
                match: g => g.Id == request.GroupId,
                includeProperties: q => q
                    .Include(g => g.YearSeason)
                    .ThenInclude(ys => ys.Season)
                    .ThenInclude(s => s.PlotCultivations)

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

            // Determine which version to query
            CultivationVersion? targetVersion = null;
            
            if (request.VersionId.HasValue)
            {
                // Query specific version
                targetVersion = plotCultivation.CultivationVersions
                    .FirstOrDefault(v => v.Id == request.VersionId.Value);
                
                if (targetVersion == null)
                {
                    return Result<CurrentPlotCultivationDetailResponse>.Failure(
                        $"Version with ID {request.VersionId.Value} not found for this plot cultivation.",
                        "VersionNotFound");
                }
            }
            else
            {
                // Get the latest version (highest VersionOrder) - default behavior
                targetVersion = plotCultivation.CultivationVersions
                    .OrderByDescending(v => v.VersionOrder)
                    .FirstOrDefault();
            }

            // 4. Load cultivation tasks with related data for the target version
            Guid? versionId = targetVersion?.Id;
            
            // DEBUG: First check raw database values without includes
            var rawTaskData = await _unitOfWork.Repository<CultivationTask>()
                .GetQueryable()
                .Where(ct => ct.PlotCultivationId == plotCultivation.Id && ct.VersionId == versionId)
                .Select(ct => new { 
                    TaskId = ct.Id, 
                    TaskName = ct.CultivationTaskName, 
                    ProductionPlanTaskId = ct.ProductionPlanTaskId,
                    ExecutionOrder = ct.ExecutionOrder,
                    Status = ct.Status,
                    IsContingency = ct.IsContingency
                })
                .ToListAsync(cancellationToken);
            
            _logger.LogInformation(
                "Raw database check for Version {VersionId}: Found {TaskCount} tasks. " +
                "Tasks with ProductionPlanTaskId: {WithPPTId}, Tasks without: {WithoutPPTId}. " +
                "Emergency tasks: {EmergencyCount}",
                versionId,
                rawTaskData.Count,
                rawTaskData.Count(t => t.ProductionPlanTaskId.HasValue),
                rawTaskData.Count(t => !t.ProductionPlanTaskId.HasValue),
                rawTaskData.Count(t => t.Status == Domain.Enums.TaskStatus.Emergency)
            );
            
            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.PlotCultivationId == plotCultivation.Id && ct.VersionId == versionId,
                orderBy: q => q.OrderBy(ct => ct.ExecutionOrder),
#pragma warning disable CS8602 // Dereference of a possibly null reference
                includeProperties: q => q
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionStage)
                            .ThenInclude(ps => ps.ProductionPlan)
                    .Include(ct => ct.CultivationTaskMaterials)
                        .ThenInclude(ctm => ctm.Material)
#pragma warning restore CS8602 // Dereference of a possibly null reference
            );

            // Log what we loaded
            _logger.LogInformation(
                "Loaded {TaskCount} tasks. Navigation property status: " +
                "ProductionPlanTask null: {PPTNullCount}, " +
                "ProductionStage null: {PSNullCount}",
                tasks.Count,
                tasks.Count(t => t.ProductionPlanTask == null),
                tasks.Count(t => t.ProductionPlanTask?.ProductionStage == null)
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

            // 5. Get production plan info from first task that has one
            var firstTaskWithPlan = uniqueTasks.FirstOrDefault(t => t.ProductionPlanTask != null);
            var productionPlan = firstTaskWithPlan?.ProductionPlanTask?.ProductionStage?.ProductionPlan;

            // DEBUG: Log task details to diagnose stage grouping issue
            _logger.LogInformation(
                "Task loading summary for Version {VersionId}: Total tasks: {TotalTasks}, " +
                "Tasks with ProductionPlanTask loaded: {LoadedCount}, " +
                "Tasks with ProductionPlanTaskId: {IdCount}",
                versionId,
                uniqueTasks.Count,
                uniqueTasks.Count(t => t.ProductionPlanTask != null),
                uniqueTasks.Count(t => t.ProductionPlanTaskId.HasValue)
            );

            // 6. Group tasks by stage - handle tasks with and without ProductionPlanTask
            var stagesGroup = new List<CultivationStageSummary>();
            
            // Group by stage ID (null for emergency tasks)
            var tasksByStage = uniqueTasks
                .GroupBy(task => task.ProductionPlanTask?.ProductionStageId)
                .OrderBy(g => {
                    var firstTask = g.First();
                    return firstTask.ProductionPlanTask?.ProductionStage?.SequenceOrder ?? int.MaxValue;
                });

            foreach (var stageTasksGroup in tasksByStage)
            {
                var firstTask = stageTasksGroup.First();
                var stage = firstTask.ProductionPlanTask?.ProductionStage;
                
                _logger.LogInformation(
                    "Processing stage group: StageId={StageId}, StageName={StageName}, TaskCount={TaskCount}, " +
                    "First task: Id={TaskId}, Name={TaskName}, HasProductionPlanTask={HasPPT}, ProductionPlanTaskId={PPTId}",
                    stage?.Id,
                    stage?.StageName ?? "Emergency Tasks",
                    stageTasksGroup.Count(),
                    firstTask.Id,
                    firstTask.CultivationTaskName,
                    firstTask.ProductionPlanTask != null,
                    firstTask.ProductionPlanTaskId
                );

                var stageSummary = new CultivationStageSummary
                {
                    StageId = stage?.Id,
                    StageName = stage?.StageName ?? "Emergency Tasks",
                    SequenceOrder = stage?.SequenceOrder ?? int.MaxValue,
                    Description = stage?.Description,
                    TypicalDurationDays = stage?.TypicalDurationDays,
                    Tasks = stageTasksGroup
                        .OrderBy(task => task.ExecutionOrder ?? int.MaxValue)
                        .ThenBy(task => task.CreatedAt)
                        .Select(task => new CultivationTaskSummary
                        {
                            TaskId = task.Id,
                            TaskName = task.CultivationTaskName ?? string.Empty,
                            TaskDescription = task.Description ?? string.Empty,
                            TaskType = task.TaskType ?? Domain.Enums.TaskType.Sowing,
                            Status = task.Status ?? Domain.Enums.TaskStatus.Draft,
                            Priority = Domain.Enums.TaskPriority.Normal,
                            PlannedStartDate = task.ProductionPlanTask?.ScheduledDate ?? null,
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
                };
                
                stagesGroup.Add(stageSummary);
            }

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
                SeasonStartDate = ParseSeasonDateToDateTime(plotCultivation.Season.StartDate, plotCultivation.PlantingDate.Year),
                SeasonEndDate = ParseSeasonDateToDateTime(plotCultivation.Season.EndDate, plotCultivation.PlantingDate.Year),

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

                ActiveVersionId = targetVersion?.Id,
                ActiveVersionName = targetVersion?.VersionName,

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
                "Successfully retrieved cultivation plan for Plot {PlotId} in Group {GroupId} for Season {SeasonId} " +
                "using version {VersionName} (Order: {VersionOrder}, Requested: {RequestedVersionId}). " +
                "Total unique tasks: {UniqueCount} (from {TotalCount} records)",
                request.PlotId, request.GroupId, group.YearSeason.SeasonId, 
                targetVersion?.VersionName, targetVersion?.VersionOrder, request.VersionId,
                uniqueTasks.Count, tasks.Count);

            return Result<CurrentPlotCultivationDetailResponse>.Success(
                response,
                request.VersionId.HasValue 
                    ? $"Successfully retrieved cultivation plan for specific version '{targetVersion?.VersionName}'."
                    : "Successfully retrieved cultivation plan with latest version.");
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

    /// <summary>
    /// Parses season date string (MM/DD format) to DateTime by combining with a year
    /// </summary>
    /// <param name="seasonDateStr">Season date in MM/DD format (e.g., "04/30")</param>
    /// <param name="year">Year to use for the date</param>
    /// <returns>DateTime representing the season date</returns>
    private DateTime ParseSeasonDateToDateTime(string seasonDateStr, int year)
    {
        try
        {
            var parts = seasonDateStr.Split('/');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid season date format: {SeasonDateStr}. Expected MM/DD format.", seasonDateStr);
                return new DateTime(year, 1, 1); // Default to January 1st
            }

            int month = int.Parse(parts[0]);
            int day = int.Parse(parts[1]);
            
            // Validate month and day ranges
            if (month < 1 || month > 12)
            {
                _logger.LogWarning("Invalid month in season date: {Month}. Using January.", month);
                month = 1;
            }
            
            if (day < 1 || day > 31)
            {
                _logger.LogWarning("Invalid day in season date: {Day}. Using 1st.", day);
                day = 1;
            }

            // Handle cases where the season crosses year boundary (e.g., Winter-Spring season)
            // If this is an end date in early months (Jan-Apr) and planting was late in previous year,
            // the end date should be in the next year
            if (month <= 4 && seasonDateStr.Contains("30") && year > DateTime.UtcNow.Year - 1)
            {
                // This might be a season ending in early next year
                return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            }

            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing season date: {SeasonDateStr} for year {Year}", seasonDateStr, year);
            // Return a safe default
            return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
