
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

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
            // Tải tất cả CultivationTasks liên quan đến PlotCultivationId
            // và bao gồm tất cả các mối quan hệ cần thiết để ánh xạ
            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.PlotCultivationId == request.PlotCultivationId,
                orderBy: q => q.OrderBy(ct => ct.ScheduledEndDate),
                includeProperties: q => q
                    .Include(ct => ct.CultivationTaskMaterials) // Vật tư thực tế
                        .ThenInclude(ctm => ctm.Material)
                    .Include(ct => ct.ProductionPlanTask) // Công việc kế hoạch gốc
                        .ThenInclude(ppt => ppt.ProductionStage) // Giai đoạn gốc
                            .ThenInclude(ps => ps.ProductionPlan) // Kế hoạch gốc
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials) // Vật tư kế hoạch
                            .ThenInclude(pptm => pptm.Material)
                    .Include(ct => ct.PlotCultivation) // Thửa đất canh tác
            );

            if (!tasks.Any())
            {
                return Result<FarmerPlanViewResponse>.Failure($"No tasks found for Plot Cultivation ID {request.PlotCultivationId}.", "TasksNotFound");
            }

            // Lấy thông tin chung từ task đầu tiên (vì tất cả đều thuộc cùng một Plan)
            var firstTask = tasks.First();
            var plan = firstTask.ProductionPlanTask.ProductionStage.ProductionPlan;
            var plotCultivation = firstTask.PlotCultivation;

            var response = new FarmerPlanViewResponse
            {
                PlotCultivationId = request.PlotCultivationId,
                ProductionPlanId = plan.Id,
                PlanName = plan.PlanName,
                BasePlantingDate = plan.BasePlantingDate,
                PlanStatus = plan.Status,
                PlotArea = plotCultivation.Area ?? 0M
            };

            // Nhóm các CultivationTasks theo ProductionStage (Giai đoạn)
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

                // Ánh xạ Công việc Canh tác
                var taskResponse = new FarmerCultivationTaskResponse
                {
                    Id = task.Id,
                    TaskName = task.CultivationTaskName ?? task.ProductionPlanTask.TaskName,
                    Description = task.Description ?? task.ProductionPlanTask.Description,
                    TaskType = task.TaskType ?? task.ProductionPlanTask.TaskType,
                    ScheduledDate = (DateTime)(task.ScheduledEndDate ?? task.ProductionPlanTask.ScheduledEndDate),
                    Status = task.Status ?? RiceProduction.Domain.Enums.TaskStatus.Draft,
                    Priority = task.ProductionPlanTask.Priority,
                    IsContingency = task.IsContingency,
                    ActualMaterialCost = task.ActualMaterialCost
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