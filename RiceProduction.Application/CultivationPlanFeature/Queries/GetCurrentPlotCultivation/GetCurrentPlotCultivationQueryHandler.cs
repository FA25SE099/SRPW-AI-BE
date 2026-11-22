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
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetCurrentPlotCultivationQueryHandler> _logger;

    public GetCurrentPlotCultivationQueryHandler(IApplicationDbContext context, ILogger<GetCurrentPlotCultivationQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<CurrentPlotCultivationDetailResponse>> Handle(GetCurrentPlotCultivationQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plot = await _context.Plots
                .AsNoTracking()
                .Include(p => p.PlotCultivations
                    .Where(pc => pc.Status == CultivationStatus.InProgress || pc.Status == CultivationStatus.Planned))
                    .ThenInclude(pc => pc.Season)
                .Include(p => p.PlotCultivations)
                    .ThenInclude(pc => pc.RiceVariety)
                .Include(p => p.PlotCultivations)
                    .ThenInclude(pc => pc.CultivationVersions)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == request.PlotId, cancellationToken);

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

            // Get the latest version (highest VersionOrder)
            var latestVersion = currentCultivation.CultivationVersions
                .OrderByDescending(v => v.VersionOrder)
                .FirstOrDefault();

            // Load cultivation tasks with related data for the latest version
            Guid? versionId = latestVersion?.Id;
            var tasks = await _context.CultivationTasks
                .AsNoTracking()
                .Where(ct => ct.PlotCultivationId == currentCultivation.Id && ct.VersionId == versionId)
#pragma warning disable CS8602 // Dereference of a possibly null reference
                .Include(ct => ct.ProductionPlanTask)!
                    .ThenInclude(ppt => ppt.ProductionStage)!
                    .ThenInclude(ps => ps.ProductionPlan)
                .Include(ct => ct.CultivationTaskMaterials)
                    .ThenInclude(ctm => ctm.Material)
#pragma warning restore CS8602 // Dereference of a possibly null reference
                .OrderBy(ct => ct.ExecutionOrder)
                .ToListAsync(cancellationToken);

            var uniqueTasks = tasks.DistinctBy(t => t.Id).ToList();
            var duplicateCount = tasks.Count - uniqueTasks.Count;
            if (duplicateCount > 0)
            {
                _logger.LogWarning(
                    "Found {DuplicateCount} duplicate tasks for PlotCultivation {PlotCultivationId} Version {VersionId}. " +
                    "This indicates a data integrity issue that should be investigated.",
                    duplicateCount, currentCultivation.Id, versionId);
            }

            var productionPlan = uniqueTasks.FirstOrDefault()?.ProductionPlanTask?.ProductionStage?.ProductionPlan;

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

                ActiveVersionId = latestVersion?.Id,
                ActiveVersionName = latestVersion?.VersionName,

                Stages = BuildStageSummaries(uniqueTasks),
                Progress = BuildProgress(uniqueTasks, currentCultivation.PlantingDate)
            };

            _logger.LogInformation(
                "Successfully retrieved current cultivation plan for plot {PlotId} using latest version {VersionOrder}. " +
                "Total unique tasks: {UniqueCount} (from {TotalCount} records)",
                request.PlotId, latestVersion?.VersionOrder, uniqueTasks.Count, tasks.Count);

            return Result<CurrentPlotCultivationDetailResponse>.Success(response, "Successfully retrieved current cultivation plan.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current cultivation plan for plot {PlotId}", request.PlotId);
            return Result<CurrentPlotCultivationDetailResponse>.Failure("An error occurred while retrieving cultivation plan.", "RetrievalFailed");
        }
    }

    private static List<CultivationStageSummary> BuildStageSummaries(List<CultivationTask> uniqueTasks) =>
        uniqueTasks
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
                        TaskType = task.TaskType ?? TaskType.Sowing,
                        Status = task.Status ?? Domain.Enums.TaskStatus.Draft,
                        Priority = TaskPriority.Normal,
                        PlannedStartDate = task.ProductionPlanTask?.ScheduledDate,
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

    private static CultivationProgress BuildProgress(List<CultivationTask> uniqueTasks, DateTime plantingDate)
    {
        var totalTasks = uniqueTasks.Count;
        var completedTasks = uniqueTasks.Count(t => t.Status == Domain.Enums.TaskStatus.Completed);
        var inProgressTasks = uniqueTasks.Count(t => t.Status == Domain.Enums.TaskStatus.InProgress);
        var pendingTasks = uniqueTasks.Count(t =>
            t.Status == Domain.Enums.TaskStatus.Draft || t.Status == Domain.Enums.TaskStatus.PendingApproval);
        var completionPercentage = totalTasks > 0 ? (decimal)completedTasks / totalTasks * 100 : 0;

        var now = DateTime.UtcNow;
        var lastTask = uniqueTasks.OrderByDescending(t => t.ScheduledEndDate).FirstOrDefault();
        int? estimatedDaysRemaining = lastTask?.ScheduledEndDate is { } lastEndDate
            ? Math.Max(0, (lastEndDate - now).Days)
            : null;

        return new CultivationProgress
        {
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            InProgressTasks = inProgressTasks,
            PendingTasks = pendingTasks,
            CompletionPercentage = Math.Round(completionPercentage, 2),
            DaysElapsed = (now - plantingDate).Days,
            EstimatedDaysRemaining = estimatedDaysRemaining
        };
    }
}
