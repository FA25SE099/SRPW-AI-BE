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

            // Get the latest version (highest VersionOrder) - updated to match GetPlotCultivationByGroupAndPlot pattern
            var latestVersion = plotCultivation.CultivationVersions
                .OrderByDescending(v => v.VersionOrder)
                .FirstOrDefault();

            if (latestVersion == null)
            {
                return Result<CultivationPlanDetailResponse>.Failure(
                    $"No version found for cultivation plan {request.PlanId}.",
                    "NoVersionFound");
            }

            // Get tasks for the latest version
            var tasks = await _unitOfWork.Repository<CultivationTask>()
                .ListAsync(
                    filter: ct => ct.PlotCultivationId == plotCultivation.Id &&
                                ct.VersionId.HasValue && ct.VersionId.Value == latestVersion.Id,
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

            // Sort tasks in memory after loading
            var sortedTasks = tasks
                .OrderBy(ct => ct.ProductionPlanTask?.ProductionStage?.SequenceOrder ?? 0)
                .ThenBy(ct => ct.ProductionPlanTask?.SequenceOrder ?? 0)
                .ToList();

            if (!sortedTasks.Any())
            {
                return Result<CultivationPlanDetailResponse>.Failure(
                    $"No tasks found for cultivation plan {request.PlanId}.",
                    "TasksNotFound");
            }

            var stagesMap = new Dictionary<Guid, CultivationStageResponse>();

            foreach (var task in sortedTasks)
            {
                if (task.ProductionPlanTask?.ProductionStage == null) continue;

                var stage = task.ProductionPlanTask.ProductionStage;

                if (!stagesMap.ContainsKey(stage.Id))
                {
                    stagesMap[stage.Id] = new CultivationStageResponse
                    {
                        Id = stage.Id,
                        StageName = stage.StageName,
                        SequenceOrder = stage.SequenceOrder,
                        ExpectedDurationDays = stage.TypicalDurationDays
                    };
                }

                var taskMaterials = task.ProductionPlanTask.ProductionPlanTaskMaterials
                    .Select(m => new CultivationTaskMaterialResponse
                    {
                        MaterialId = m.MaterialId,
                        MaterialName = m.Material.Name,
                        QuantityPerHa = m.QuantityPerHa,
                        Unit = m.Material.Unit
                    }).ToList();

                stagesMap[stage.Id].Tasks.Add(new CultivationTaskResponse
                {
                    Id = task.ProductionPlanTask.Id,
                    TaskName = task.ProductionPlanTask.TaskName,
                    Description = task.ProductionPlanTask.Description,
                    TaskType = task.ProductionPlanTask.TaskType.ToString(),
                    ScheduledDate = task.ProductionPlanTask.ScheduledDate,
                    ScheduledEndDate = task.ProductionPlanTask.ScheduledEndDate,
                    Priority = task.ProductionPlanTask.Priority.ToString(),
                    SequenceOrder = task.ProductionPlanTask.SequenceOrder,
                    Materials = taskMaterials
                });
            }

            var response = new CultivationPlanDetailResponse
            {
                Id = plotCultivation.Id,
                PlotId = plotCultivation.PlotId,
                PlotName = $"{plotCultivation.Plot.SoThua}/{plotCultivation.Plot.SoTo}",
                PlanName = $"Plan {plotCultivation.PlantingDate:yyyy-MM-dd}",
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

            return Result<CultivationPlanDetailResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cultivation plan {PlanId}", request.PlanId);
            return Result<CultivationPlanDetailResponse>.Failure("An error occurred while retrieving the cultivation plan.");
        }
    }
}

