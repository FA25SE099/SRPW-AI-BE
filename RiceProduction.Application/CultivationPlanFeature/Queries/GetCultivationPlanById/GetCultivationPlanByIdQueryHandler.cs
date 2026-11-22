using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationPlanById;

public class GetCultivationPlanByIdQueryHandler : IRequestHandler<GetCultivationPlanByIdQuery, Result<CultivationPlanDetailResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetCultivationPlanByIdQueryHandler> _logger;

    public GetCultivationPlanByIdQueryHandler(
        IApplicationDbContext context,
        ILogger<GetCultivationPlanByIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<CultivationPlanDetailResponse>> Handle(
        GetCultivationPlanByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get plot cultivation with related data
            var plotCultivation = await _context.PlotCultivations
                .AsNoTracking()
                .AsSplitQuery()
                .Include(pc => pc.Plot)
                    .ThenInclude(p => p!.Farmer)
                .Include(pc => pc.Plot)
                    .ThenInclude(p => p!.GroupPlots)
                        .ThenInclude(gp => gp.Group)
                            .ThenInclude(g => g!.Cluster)
                .Include(pc => pc.RiceVariety)
                .Include(pc => pc.CultivationVersions)
                .FirstOrDefaultAsync(pc => pc.Id == request.PlanId, cancellationToken);

            if (plotCultivation == null)
            {
                return Result<CultivationPlanDetailResponse>.Failure(
                    $"Cultivation plan with ID {request.PlanId} not found.",
                    "NotFound");
            }

            // Get the latest version (highest VersionOrder) - matches GetPlotCultivationByGroupAndPlot pattern
            var latestVersion = plotCultivation.CultivationVersions
                .OrderByDescending(v => v.VersionOrder)
                .FirstOrDefault();

            if (latestVersion == null)
            {
                return Result<CultivationPlanDetailResponse>.Failure(
                    $"No version found for cultivation plan {request.PlanId}.",
                    "NoVersionFound");
            }

            _logger.LogInformation(
                "Loading cultivation plan {PlanId} with version {VersionName} (Order: {VersionOrder})",
                request.PlanId, latestVersion.VersionName, latestVersion.VersionOrder);

            // Get tasks for the latest version
            var tasks = await _context.CultivationTasks
                .AsNoTracking()
                .AsSplitQuery()
#pragma warning disable CS8602 // Dereference of a possibly null reference
                .Include(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt!.ProductionStage)
                .Include(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt!.ProductionPlanTaskMaterials)
                        .ThenInclude(pptm => pptm.Material)
                .Include(ct => ct.CultivationTaskMaterials)
                    .ThenInclude(ctm => ctm.Material)
#pragma warning restore CS8602 // Dereference of a possibly null reference
                .Where(ct => ct.PlotCultivationId == plotCultivation.Id &&
                             ct.VersionId == latestVersion.Id)
                .ToListAsync(cancellationToken);

            if (!tasks.Any())
            {
                _logger.LogWarning(
                    "No tasks found for cultivation plan {PlanId} version {VersionName}",
                    request.PlanId, latestVersion.VersionName);
                    
                return Result<CultivationPlanDetailResponse>.Failure(
                    $"No tasks found for cultivation plan {request.PlanId}.",
                    "TasksNotFound");
            }

            var totalArea = plotCultivation.Area ?? plotCultivation.Plot.Area;
            var stages = BuildStages(tasks, totalArea);

            var response = new CultivationPlanDetailResponse
            {
                Id = plotCultivation.Id,
                PlotId = plotCultivation.PlotId,
                PlotName = $"{plotCultivation.Plot.SoThua}/{plotCultivation.Plot.SoTo}",
                PlanName = $"Plan {plotCultivation.PlantingDate:yyyy-MM-dd} - {latestVersion.VersionName}",
                RiceVarietyId = plotCultivation.RiceVarietyId,
                RiceVarietyName = plotCultivation.RiceVariety?.VarietyName ?? "Unknown",
                BasePlantingDate = plotCultivation.PlantingDate,
                TotalArea = totalArea,
                Status = plotCultivation.Status.ToString(),
                EstimatedTotalCost = 0,
                FarmerName = plotCultivation.Plot.Farmer?.FullName,
                ClusterName = plotCultivation.Plot.GroupPlots.FirstOrDefault()?.Group?.Cluster?.ClusterName,
                Stages = stages
            };

            _logger.LogInformation(
                "Successfully retrieved cultivation plan {PlanId} with {StageCount} stages and {TaskCount} tasks (Version: {VersionName})",
                request.PlanId, response.Stages.Count, tasks.Count, latestVersion.VersionName);

            return Result<CultivationPlanDetailResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cultivation plan {PlanId}", request.PlanId);
            return Result<CultivationPlanDetailResponse>.Failure("An error occurred while retrieving the cultivation plan.");
        }
    }

    private static List<CultivationStageResponse> BuildStages(
        IEnumerable<CultivationTask> tasks,
        decimal totalArea)
    {
        var stagesMap = new Dictionary<Guid, CultivationStageResponse>();

        var sortedTasks = tasks
            .OrderBy(ct => ct.ProductionPlanTask?.ProductionStage?.SequenceOrder ?? int.MaxValue)
            .ThenBy(ct => ct.ExecutionOrder ?? int.MaxValue)
            .ThenBy(ct => ct.CreatedAt);

        foreach (var task in sortedTasks)
        {
            var taskMaterials = task.CultivationTaskMaterials
                .Select(ctm => new CultivationTaskMaterialResponse
                {
                    MaterialId = ctm.MaterialId,
                    MaterialName = ctm.Material.Name,
                    QuantityPerHa = ctm.ActualQuantity / totalArea,
                    Unit = ctm.Material.Unit
                })
                .ToList();

            var stage = task.ProductionPlanTask?.ProductionStage;
            if (stage != null)
            {
                if (!stagesMap.TryGetValue(stage.Id, out var stageResponse))
                {
                    stageResponse = new CultivationStageResponse
                    {
                        Id = stage.Id,
                        StageName = stage.StageName,
                        SequenceOrder = stage.SequenceOrder,
                        ExpectedDurationDays = stage.TypicalDurationDays,
                        Tasks = new List<CultivationTaskResponse>()
                    };
                    stagesMap[stage.Id] = stageResponse;
                }

                var planTask = task.ProductionPlanTask!;
                stageResponse.Tasks.Add(new CultivationTaskResponse
                {
                    Id = task.Id,
                    TaskName = task.CultivationTaskName ?? planTask.TaskName,
                    Description = task.Description ?? planTask.Description,
                    TaskType = task.TaskType?.ToString() ?? planTask.TaskType.ToString(),
                    ScheduledDate = planTask.ScheduledDate,
                    ScheduledEndDate = task.ScheduledEndDate ?? planTask.ScheduledEndDate,
                    ActualEndDate = task.ActualEndDate,
                    ActualStartDate = task.ActualStartDate,
                    TaskStatus = task.Status?.ToString() ?? "Approved",
                    Priority = planTask.Priority.ToString(),
                    SequenceOrder = task.ExecutionOrder ?? planTask.SequenceOrder,
                    Materials = taskMaterials
                });
            }
            else
            {
                if (!stagesMap.TryGetValue(Guid.Empty, out var emergencyStage))
                {
                    emergencyStage = new CultivationStageResponse
                    {
                        Id = Guid.Empty,
                        StageName = "Emergency Tasks",
                        SequenceOrder = int.MaxValue,
                        ExpectedDurationDays = null,
                        Tasks = new List<CultivationTaskResponse>()
                    };
                    stagesMap[Guid.Empty] = emergencyStage;
                }

                emergencyStage.Tasks.Add(new CultivationTaskResponse
                {
                    Id = task.Id,
                    TaskName = task.CultivationTaskName ?? "Emergency Task",
                    Description = task.Description,
                    TaskType = task.TaskType?.ToString() ?? "PestControl",
                    ScheduledDate = null,
                    ScheduledEndDate = task.ScheduledEndDate,
                    ActualEndDate = task.ActualEndDate,
                    ActualStartDate = task.ActualStartDate,
                    TaskStatus = task.Status?.ToString() ?? "Emergency",
                    Priority = "High",
                    SequenceOrder = task.ExecutionOrder ?? 0,
                    Materials = taskMaterials
                });
            }
        }

        return stagesMap.Values.OrderBy(s => s.SequenceOrder).ToList();
    }
}

