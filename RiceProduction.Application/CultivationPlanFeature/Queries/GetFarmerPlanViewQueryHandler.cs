using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.CultivationPlanFeature.Queries;

public class GetFarmerPlanViewQueryHandler :
    IRequestHandler<GetFarmerPlanViewQuery, Result<FarmerPlanViewResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetFarmerPlanViewQueryHandler> _logger;

    public GetFarmerPlanViewQueryHandler(IApplicationDbContext context, ILogger<GetFarmerPlanViewQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<FarmerPlanViewResponse>> Handle(GetFarmerPlanViewQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plotCultivation = await _context.PlotCultivations
                .AsNoTracking()
                .Include(pc => pc.CultivationVersions)
                .Include(pc => pc.Plot)
                .FirstOrDefaultAsync(pc => pc.Id == request.PlotCultivationId, cancellationToken);

            if (plotCultivation == null)
            {
                return Result<FarmerPlanViewResponse>.Failure($"Plot Cultivation with ID {request.PlotCultivationId} not found.", "PlotCultivationNotFound");
            }
            
            // Get the latest version (highest VersionOrder) - updated to match GetPlotCultivationByGroupAndPlot pattern
            var latestVersion = plotCultivation.CultivationVersions
                .OrderByDescending(v => v.VersionOrder)
                .FirstOrDefault();

            var latestVersionId = latestVersion?.Id;
            var latestVersionName = latestVersion?.VersionName ?? "Original";

            if (!latestVersionId.HasValue)
            {
                _logger.LogWarning("No version found for PlotCultivation {PCId}", request.PlotCultivationId);
            }
            
            var tasks = await _context.CultivationTasks
                .AsNoTracking()
                .Where(ct =>
                    ct.PlotCultivationId == request.PlotCultivationId &&
                    ct.VersionId == latestVersionId)
#pragma warning disable CS8602 // Dereference of a possibly null reference
                .Include(ct => ct.CultivationTaskMaterials)
                    .ThenInclude(ctm => ctm.Material)
                .Include(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt.ProductionStage)
                        .ThenInclude(ps => ps.ProductionPlan)
                .Include(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
                        .ThenInclude(pptm => pptm.Material)
#pragma warning restore CS8602 // Dereference of a possibly null reference
                .AsSplitQuery()
                .OrderBy(ct => ct.ProductionPlanTask!.ProductionStage!.SequenceOrder)
                .ThenBy(ct => ct.ExecutionOrder)
                .ToListAsync(cancellationToken);

            if (tasks.Count == 0)
            {
                return Result<FarmerPlanViewResponse>.Failure($"No tasks found for Plot Cultivation ID {request.PlotCultivationId}.", "TasksNotFound");
            }

            var firstTask = tasks.First();
            var plan = firstTask.ProductionPlanTask?.ProductionStage?.ProductionPlan;

            if (plan == null)
            {
                return Result<FarmerPlanViewResponse>.Failure($"Production plan not found for Plot Cultivation ID {request.PlotCultivationId}.", "PlanNotFound");
            }

            var response = new FarmerPlanViewResponse
            {
                PlotCultivationId = request.PlotCultivationId,
                ProductionPlanId = plan.Id,
                PlanName = plan.PlanName,
                BasePlantingDate = plan.BasePlantingDate,
                PlanStatus = plan.Status,
                PlotArea = plotCultivation.Area ?? 0M,
                ActiveVersionName = latestVersionName
            };

            var stagesMap = new Dictionary<Guid, FarmerPlanStageViewResponse>();

            foreach (var task in tasks)
            {
                var stage = task.ProductionPlanTask?.ProductionStage;
                
                if (stage == null) continue;
                
                if (!stagesMap.ContainsKey(stage.Id))
                {
                    stagesMap[stage.Id] = new FarmerPlanStageViewResponse
                    {
                        StageName = stage.StageName,
                        SequenceOrder = stage.SequenceOrder
                    };
                }

                var taskResponse = new FarmerCultivationTaskResponse
                {
                    Id = task.Id,
                    TaskName = task.CultivationTaskName ?? task.ProductionPlanTask!.TaskName,
                    Description = task.Description ?? task.ProductionPlanTask!.Description,
                    TaskType = task.TaskType.GetValueOrDefault(TaskType.Harvesting),
                    ScheduledDate = task.ProductionPlanTask!.ScheduledDate,
                    Status = task.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft),
                    Priority = task.ProductionPlanTask!.Priority,
                    IsContingency = task.IsContingency,
                    ActualMaterialCost = task.ActualMaterialCost,
                    VersionName = latestVersionName
                };

                var materialsMap = new Dictionary<Guid, FarmerMaterialComparisonResponse>();

                foreach (var taskMaterial in task.CultivationTaskMaterials)
                {
                    if (!materialsMap.TryGetValue(taskMaterial.MaterialId, out var comparison))
                    {
                        comparison = new FarmerMaterialComparisonResponse
                        {
                            MaterialId = taskMaterial.MaterialId,
                            MaterialName = taskMaterial.Material.Name,
                            MaterialUnit = taskMaterial.Material.Unit
                        };
                        materialsMap[taskMaterial.MaterialId] = comparison;
                    }

                    comparison.PlannedQuantityPerHa = Math.Ceiling(taskMaterial.ActualQuantity / plotCultivation.Plot.Area);
                    comparison.PlannedEstimatedAmount = taskMaterial.ActualCost;
                    comparison.ActualQuantity = taskMaterial.ActualQuantity;
                    comparison.ActualCost = taskMaterial.ActualCost;
                }

                taskResponse.Materials = materialsMap.Values.ToList();
                stagesMap[stage.Id].Tasks.Add(taskResponse);
            }

            response.Stages = stagesMap.Values.OrderBy(s => s.SequenceOrder).ToList();

            _logger.LogInformation("Successfully retrieved plan view for Farmer for PlotCultivationId {PlotCultivationId}", request.PlotCultivationId);

            return Result<FarmerPlanViewResponse>.Success(response, "Successfully retrieved farmer plan view.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving farmer plan view for PlotCultivationId {PlotCultivationId}", request.PlotCultivationId);
            return Result<FarmerPlanViewResponse>.Failure("An error occurred while retrieving the plan view.", "GetFarmerPlanViewFailed");
        }
    }
}