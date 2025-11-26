
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.CultivationPlanFeature.Queries;
public class GetFarmerPlanViewQueryHandler :
    IRequestHandler<GetFarmerPlanViewQuery, Result<FarmerPlanViewResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetFarmerPlanViewQueryHandler> _logger;

    public GetFarmerPlanViewQueryHandler(IUnitOfWork unitOfWork, ILogger<GetFarmerPlanViewQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<FarmerPlanViewResponse>> Handle(GetFarmerPlanViewQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Tải PlotCultivation và Active Version
            var plotCultivation = await _unitOfWork.Repository<PlotCultivation>().FindAsync(
                pc => pc.Id == request.PlotCultivationId,
                includeProperties: q => q
                    .Include(pc => pc.CultivationVersions.Where(v => v.IsActive)) // Load Active Version
                    .Include(pc => pc.Plot)
            );

            if (plotCultivation == null)
            {
                return Result<FarmerPlanViewResponse>.Failure($"Plot Cultivation with ID {request.PlotCultivationId} not found.", "PlotCultivationNotFound");
            }
            
            var activeVersion = plotCultivation.CultivationVersions.FirstOrDefault();
            var activeVersionId = activeVersion?.Id;
            var activeVersionName = activeVersion?.VersionName ?? "Original";

            if (!activeVersionId.HasValue)
            {
                // Xử lý trường hợp không có Version nào được đánh dấu là Active (có thể là Original Plan)
                _logger.LogWarning("No active version found for PlotCultivation {PCId}", request.PlotCultivationId);
            }
            
            // 2. Tải CultivationTasks chỉ thuộc về Version đang hoạt động/mới nhất
            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => 
                    ct.PlotCultivationId == request.PlotCultivationId && 
                    (!activeVersionId.HasValue || ct.VersionId == activeVersionId.Value), // Lọc theo VersionId nếu có
                orderBy: q => q.OrderBy(ct => ct.ProductionPlanTask.ProductionStage.SequenceOrder)
                              .ThenBy(ct => ct.ExecutionOrder),
                includeProperties: q => q
                    .Include(ct => ct.CultivationTaskMaterials).ThenInclude(ctm => ctm.Material) 
                    .Include(ct => ct.ProductionPlanTask).ThenInclude(ppt => ppt.ProductionStage).ThenInclude(ps => ps.ProductionPlan)
                    .Include(ct => ct.ProductionPlanTask).ThenInclude(ppt => ppt.ProductionPlanTaskMaterials).ThenInclude(pptm => pptm.Material)
            );

            if (!tasks.Any())
            {
                return Result<FarmerPlanViewResponse>.Failure($"No active tasks found for Plot Cultivation ID {request.PlotCultivationId}.", "TasksNotFound");
            }

            var firstTask = tasks.First();
            var plan = firstTask.ProductionPlanTask.ProductionStage.ProductionPlan;

            var response = new FarmerPlanViewResponse
            {
                PlotCultivationId = request.PlotCultivationId,
                ProductionPlanId = plan.Id,
                PlanName = plan.PlanName,
                BasePlantingDate = plan.BasePlantingDate,
                PlanStatus = plan.Status,
                PlotArea = plotCultivation.Area ?? 0M,
                ActiveVersionName = activeVersionName
            };

            var stagesMap = new Dictionary<Guid, FarmerPlanStageViewResponse>();

            foreach (var task in tasks)
            {
                var stage = task.ProductionPlanTask.ProductionStage;
                
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
                    TaskName = task.CultivationTaskName ?? task.ProductionPlanTask.TaskName,
                    Description = task.Description ?? task.ProductionPlanTask.Description,
                    TaskType = task.TaskType.GetValueOrDefault(TaskType.Harvesting),
                    ScheduledDate = task.ProductionPlanTask.ScheduledDate,
                    Status = task.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft),
                    Priority = task.ProductionPlanTask.Priority,
                    IsContingency = task.IsContingency,
                    ActualMaterialCost = task.ActualMaterialCost,
                    VersionName = activeVersionName // Gán tên Version
                };

                // Ánh xạ và So sánh Vật tư
                var materialsMap = new Dictionary<Guid, FarmerMaterialComparisonResponse>();

                // 1. Thêm vật tư Kế hoạch (Planned)
                foreach (var plannedMat in task.ProductionPlanTask.ProductionPlanTaskMaterials)
                {
                    if (!materialsMap.ContainsKey(plannedMat.MaterialId))
                    {
                        materialsMap[plannedMat.MaterialId] = new FarmerMaterialComparisonResponse
                        {
                            MaterialId = plannedMat.MaterialId,
                            MaterialName = plannedMat.Material.Name,
                            MaterialUnit = plannedMat.Material.Unit
                        };
                    }
                    materialsMap[plannedMat.MaterialId].PlannedQuantityPerHa = plannedMat.QuantityPerHa;
                    materialsMap[plannedMat.MaterialId].PlannedEstimatedAmount = plannedMat.EstimatedAmount.GetValueOrDefault(0);
                }

                // 2. Thêm vật tư Thực tế (Actual)
                foreach (var actualMat in task.CultivationTaskMaterials)
                {
                    if (!materialsMap.ContainsKey(actualMat.MaterialId))
                    {
                        materialsMap[actualMat.MaterialId] = new FarmerMaterialComparisonResponse
                        {
                            MaterialId = actualMat.MaterialId,
                            MaterialName = actualMat.Material.Name,
                            MaterialUnit = actualMat.Material.Unit
                        };
                    }
                    materialsMap[actualMat.MaterialId].ActualQuantity = actualMat.ActualQuantity;
                    materialsMap[actualMat.MaterialId].ActualCost = actualMat.ActualCost;
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