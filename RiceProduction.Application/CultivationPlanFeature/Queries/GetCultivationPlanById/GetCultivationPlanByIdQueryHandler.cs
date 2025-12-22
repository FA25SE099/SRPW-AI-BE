using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationPlanById;

public class GetCultivationPlanByIdQueryHandler : IRequestHandler<GetCultivationPlanByIdQuery, Result<CultivationPlanDetailResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetCultivationPlanByIdQueryHandler> _logger;

    public GetCultivationPlanByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetCultivationPlanByIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CultivationPlanDetailResponse>> Handle(
        GetCultivationPlanByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get plot cultivation with related data
            var plotCultivation = await _unitOfWork.Repository<PlotCultivation>()
                .FindAsync(
                    match: pc => pc.Id == request.PlanId,
                    includeProperties: query => query
                        .Include(pc => pc.Plot)
                            .ThenInclude(p => p!.Farmer)
                        .Include(pc => pc.Plot)
                            .ThenInclude(p => p!.GroupPlots)
                                .ThenInclude(gp => gp.Group)
                                    .ThenInclude(g => g!.Cluster)
                        .Include(pc => pc.RiceVariety)
                        .Include(pc => pc.CultivationVersions));

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
            var tasks = await _unitOfWork.Repository<CultivationTask>()
                .ListAsync(
                    filter: ct => ct.PlotCultivationId == plotCultivation.Id && 
                                  ct.VersionId == latestVersion.Id,
#pragma warning disable CS8602 // Dereference of a possibly null reference
                    includeProperties: query => query
                        .Include(ct => ct.ProductionPlanTask)
                            .ThenInclude(ppt => ppt!.ProductionStage)
                        .Include(ct => ct.ProductionPlanTask)
                            .ThenInclude(ppt => ppt!.ProductionPlanTaskMaterials)
                                .ThenInclude(pptm => pptm.Material)
                        .Include(ct => ct.CultivationTaskMaterials)
                            .ThenInclude(ctm => ctm.Material));
#pragma warning restore CS8602 // Dereference of a possibly null reference

            if (!tasks.Any())
            {
                _logger.LogWarning(
                    "No tasks found for cultivation plan {PlanId} version {VersionName}",
                    request.PlanId, latestVersion.VersionName);
                    
                return Result<CultivationPlanDetailResponse>.Failure(
                    $"No tasks found for cultivation plan {request.PlanId}.",
                    "TasksNotFound");
            }

            // Group tasks by stage and sort by ExecutionOrder (matches GetPlotCultivationByGroupAndPlot)
            var stagesMap = new Dictionary<Guid, CultivationStageResponse>();

            // Sort by ExecutionOrder within each stage
            var sortedTasks = tasks
                .OrderBy(ct => ct.ProductionPlanTask?.ProductionStage?.SequenceOrder ?? int.MaxValue)
                .ThenBy(ct => ct.ExecutionOrder ?? int.MaxValue)
                .ThenBy(ct => ct.CreatedAt)
                .ToList();

            foreach (var task in sortedTasks)
            {
                // Handle tasks with ProductionPlanTask (regular tasks)
                if (task.ProductionPlanTask?.ProductionStage != null)
                {
                    var stage = task.ProductionPlanTask.ProductionStage;

                    if (!stagesMap.ContainsKey(stage.Id))
                    {
                        stagesMap[stage.Id] = new CultivationStageResponse
                        {
                            Id = stage.Id,
                            StageName = stage.StageName,
                            SequenceOrder = stage.SequenceOrder,
                            ExpectedDurationDays = stage.TypicalDurationDays,
                            Tasks = new List<CultivationTaskResponse>()
                        };
                    }

                    var taskMaterials = task.CultivationTaskMaterials
                        .Select(ctm => new CultivationTaskMaterialResponse
                        {
                            MaterialId = ctm.MaterialId,
                            MaterialName = ctm.Material.Name,
                            QuantityPerHa = ctm.ActualQuantity / (plotCultivation.Area ?? plotCultivation.Plot.Area),
                            Unit = ctm.Material.Unit
                        }).ToList();

                    stagesMap[stage.Id].Tasks.Add(new CultivationTaskResponse
                    {
                        Id = task.Id,
                        TaskName = task.CultivationTaskName ?? task.ProductionPlanTask.TaskName,
                        Description = task.Description ?? task.ProductionPlanTask.Description,
                        TaskType = task.TaskType?.ToString() ?? task.ProductionPlanTask.TaskType.ToString(),
                        ScheduledDate = task.ProductionPlanTask.ScheduledDate,
                        ScheduledEndDate = task.ScheduledEndDate ?? task.ProductionPlanTask.ScheduledEndDate,
                        TaskStatus = task.Status?.ToString() ?? "Approved",
                        Priority = task.ProductionPlanTask.Priority.ToString(),
                        SequenceOrder = task.ExecutionOrder ?? task.ProductionPlanTask.SequenceOrder,
                        Materials = taskMaterials
                    });
                }
                // Handle emergency tasks without ProductionPlanTask
                else
                {
                    var emergencyStageId = Guid.Empty;
                    
                    if (!stagesMap.ContainsKey(emergencyStageId))
                    {
                        stagesMap[emergencyStageId] = new CultivationStageResponse
                        {
                            Id = emergencyStageId,
                            StageName = "Emergency Tasks",
                            SequenceOrder = int.MaxValue,
                            ExpectedDurationDays = null,
                            Tasks = new List<CultivationTaskResponse>()
                        };
                    }

                    var taskMaterials = task.CultivationTaskMaterials
                        .Select(ctm => new CultivationTaskMaterialResponse
                        {
                            MaterialId = ctm.MaterialId,
                            MaterialName = ctm.Material.Name,
                            QuantityPerHa = ctm.ActualQuantity / (plotCultivation.Area ?? plotCultivation.Plot.Area),
                            Unit = ctm.Material.Unit
                        }).ToList();

                    stagesMap[emergencyStageId].Tasks.Add(new CultivationTaskResponse
                    {
                        Id = task.Id,
                        TaskName = task.CultivationTaskName ?? "Emergency Task",
                        Description = task.Description,
                        TaskType = task.TaskType?.ToString() ?? "PestControl",
                        ScheduledDate = null,
                        ScheduledEndDate = task.ScheduledEndDate,
                        TaskStatus = task.Status?.ToString() ?? "Emergency",
                        Priority = "High",
                        SequenceOrder = task.ExecutionOrder ?? 0,
                        Materials = taskMaterials
                    });
                }
            }

            var response = new CultivationPlanDetailResponse
            {
                Id = plotCultivation.Id,
                PlotId = plotCultivation.PlotId,
                PlotName = $"{plotCultivation.Plot.SoThua}/{plotCultivation.Plot.SoTo}",
                PlanName = $"Plan {plotCultivation.PlantingDate:yyyy-MM-dd} - {latestVersion.VersionName}",
                RiceVarietyId = plotCultivation.RiceVarietyId,
                RiceVarietyName = plotCultivation.RiceVariety?.VarietyName ?? "Unknown",
                BasePlantingDate = plotCultivation.PlantingDate,
                TotalArea = plotCultivation.Area ?? plotCultivation.Plot.Area,
                Status = plotCultivation.Status.ToString(),
                EstimatedTotalCost = 0,
                FarmerName = plotCultivation.Plot.Farmer?.FullName,
                ClusterName = plotCultivation.Plot.GroupPlots.FirstOrDefault()?.Group?.Cluster?.ClusterName,
                Stages = stagesMap.Values.OrderBy(s => s.SequenceOrder).ToList()
            };

            _logger.LogInformation(
                "Successfully retrieved cultivation plan {PlanId} with {StageCount} stages and {TaskCount} tasks (Version: {VersionName})",
                request.PlanId, response.Stages.Count, sortedTasks.Count, latestVersion.VersionName);

            return Result<CultivationPlanDetailResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cultivation plan {PlanId}", request.PlanId);
            return Result<CultivationPlanDetailResponse>.Failure("An error occurred while retrieving the cultivation plan.");
        }
    }
}

