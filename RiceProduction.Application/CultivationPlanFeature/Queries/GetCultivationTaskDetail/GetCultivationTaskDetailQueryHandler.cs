using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationTaskDetail;

public class GetCultivationTaskDetailQueryHandler : 
    IRequestHandler<GetCultivationTaskDetailQuery, Result<CultivationTaskDetailResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetCultivationTaskDetailQueryHandler> _logger;

    public GetCultivationTaskDetailQueryHandler(IApplicationDbContext context, ILogger<GetCultivationTaskDetailQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<CultivationTaskDetailResponse>> Handle(GetCultivationTaskDetailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _context.CultivationTasks
                .AsNoTracking()
                .Include(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .Include(ct => ct.Version)
                .Include(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
                        .ThenInclude(pptm => pptm.Material)
                .Include(ct => ct.CultivationTaskMaterials)
                    .ThenInclude(ctm => ctm.Material)
                .Include(ct => ct.FarmLogs.OrderByDescending(fl => fl.LoggedDate))
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct => ct.Id == request.CultivationTaskId, cancellationToken);

            if (task == null)
            {
                return Result<CultivationTaskDetailResponse>.Failure("Cultivation Task not found or unauthorized.", "Unauthorized");
            }

            var plot = task.PlotCultivation.Plot;
            var plannedTask = task.ProductionPlanTask;
            var plotArea = task.PlotCultivation.Area.GetValueOrDefault(0M);

            // 2. Ánh xạ chi tiết vật tư - Fixed to handle emergency tasks correctly
            List<TaskMaterialDetailResponse> materialDetails;
            
            if (task.IsContingency || task.ProductionPlanTaskId == null)
            {
                // For contingency/emergency tasks or tasks without ProductionPlanTask,
                // only show materials from CultivationTaskMaterials (actual materials used)
                materialDetails = task.CultivationTaskMaterials
                    .Select(ctm => new TaskMaterialDetailResponse
                    {
                        MaterialId = ctm.MaterialId,
                        MaterialName = ctm.Material.Name,
                        MaterialUnit = ctm.Material.Unit,
                        
                        PlannedQuantityPerHa = 0M, // Emergency tasks don't have planned quantities per ha
                        PlannedTotalEstimatedCost = 0M,
                        
                        ActualQuantityUsed = ctm.ActualQuantity,
                        ActualCost = ctm.ActualCost,
                        LogNotes = ctm.Notes
                    })
                    .ToList();
            }
            else
            {
                // For normal tasks with ProductionPlanTask, show both planned and actual materials
                var allMaterialIds = task.CultivationTaskMaterials
                    .Select(ctm => ctm.MaterialId)
                    .Union(plannedTask.ProductionPlanTaskMaterials.Select(pptm => pptm.MaterialId))
                    .Distinct();

                materialDetails = allMaterialIds.Select(materialId =>
                {
                    var actualMat = task.CultivationTaskMaterials.FirstOrDefault(ctm => ctm.MaterialId == materialId);
                    var plannedMat = plannedTask.ProductionPlanTaskMaterials.FirstOrDefault(pptm => pptm.MaterialId == materialId);
                    
                    // Use actual material if exists, otherwise fallback to planned
                    var material = actualMat?.Material ?? plannedMat?.Material;
                    
                    return new TaskMaterialDetailResponse
                    {
                        MaterialId = materialId,
                        MaterialName = material?.Name ?? string.Empty,
                        MaterialUnit = material?.Unit ?? string.Empty,
                        
                        PlannedQuantityPerHa = plannedMat?.QuantityPerHa ?? 0M,
                        PlannedTotalEstimatedCost = plannedMat?.EstimatedAmount.GetValueOrDefault(0M) ?? 0M,
                        
                        ActualQuantityUsed = actualMat?.ActualQuantity ?? 0M,
                        ActualCost = actualMat?.ActualCost ?? 0M,
                        LogNotes = actualMat?.Notes
                    };
                }).ToList();
            }

            // 3. Ánh xạ các Farm Log
            var logsResponse = task.FarmLogs.Select(fl => new FarmLogSummaryResponse
            {
                FarmLogId = fl.Id,
                LoggedDate = fl.LoggedDate,
                CompletionPercentage = fl.CompletionPercentage,
                ActualAreaCovered = fl.ActualAreaCovered,
                WorkDescription = fl.WorkDescription,
                PhotoUrls = fl.PhotoUrls,
                ActualServiceCost = fl.ServiceCost
            }).ToList();

            // 4. Tạo Response cuối cùng
            var response = new CultivationTaskDetailResponse
            {
                CultivationTaskId = task.Id,
                PlotCultivationId = task.PlotCultivationId,
                TaskName = task.CultivationTaskName ?? plannedTask.TaskName,
                Description = task.Description ?? plannedTask.Description,
                TaskType = task.TaskType.GetValueOrDefault(TaskType.Harvesting),
                Status = task.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft),
                Priority = plannedTask.Priority,
                IsContingency = task.IsContingency,
                
                // Thông tin Version
                VersionName = task.Version?.VersionName ?? "Original",
                VersionOrder = task.Version?.VersionOrder ?? 1,
                
                PlannedScheduledDate = plannedTask.ScheduledDate,
                PlannedScheduledEndDate = plannedTask.ScheduledEndDate.Value,

                ActualStartDate = task.ActualStartDate,
                ActualEndDate = task.ActualEndDate,

                EstimatedMaterialCost = plannedTask.EstimatedMaterialCost,
                ActualMaterialCost = task.ActualMaterialCost,
                ActualServiceCost = task.ActualServiceCost,
                
                PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                PlotArea = plotArea,

                Materials = materialDetails,
                FarmLogs = logsResponse
            };

            _logger.LogInformation("Successfully retrieved detail for Cultivation Task {TaskId}", request.CultivationTaskId);
            return Result<CultivationTaskDetailResponse>.Success(response, "Successfully retrieved cultivation task details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Cultivation Task details for ID {TaskId}", request.CultivationTaskId);
            return Result<CultivationTaskDetailResponse>.Failure("Failed to retrieve task details.", "GetTaskDetailFailed");
        }
    }
}