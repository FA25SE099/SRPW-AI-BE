using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlotImplementation;

public class GetPlotImplementationQueryHandler : IRequestHandler<GetPlotImplementationQuery, Result<PlotImplementationResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlotImplementationQueryHandler> _logger;

    public GetPlotImplementationQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetPlotImplementationQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PlotImplementationResponse>> Handle(GetPlotImplementationQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plot = await _unitOfWork.Repository<Plot>().FindAsync(
                match: p => p.Id == request.PlotId,
                includeProperties: q => q
                    .Include(p => p.Farmer)
                    .Include(p => p.PlotCultivations)
                        .ThenInclude(pc => pc.Season)
                    .Include(p => p.PlotCultivations)
                        .ThenInclude(pc => pc.RiceVariety)
            );

            if (plot == null)
            {
                return Result<PlotImplementationResponse>.Failure($"Plot with ID {request.PlotId} not found.", "PlotNotFound");
            }

            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(p => p.Id == request.ProductionPlanId);
            
            if (plan == null)
            {
                return Result<PlotImplementationResponse>.Failure($"Production Plan with ID {request.ProductionPlanId} not found.", "PlanNotFound");
            }

            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.PlotCultivation.PlotId == request.PlotId
                           && ct.ProductionPlanTask.ProductionStage.ProductionPlanId == request.ProductionPlanId,
                orderBy: q => q.OrderBy(ct => ct.ExecutionOrder),
                includeProperties: q => q
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
                            .ThenInclude(pptm => pptm.Material)
                    .Include(ct => ct.CultivationTaskMaterials)
                        .ThenInclude(ctm => ctm.Material)
                    .Include(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.Season)
                    .Include(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.RiceVariety)
            );

            var tasksList = tasks.ToList();
            
            var plotCultivation = tasksList.FirstOrDefault()?.PlotCultivation;
            
            var totalTasks = tasksList.Count;
            var completedTasks = tasksList.Count(t => t.Status == TaskStatus.Completed);
            var inProgressTasks = tasksList.Count(t => t.Status == TaskStatus.InProgress);
            var pendingTasks = tasksList.Count(t => t.Status == TaskStatus.Draft || t.Status == TaskStatus.PendingApproval);
            
            var completionPercentage = totalTasks > 0 ? (decimal)completedTasks / totalTasks * 100 : 0;

            var taskDetails = tasksList.Select(ct => new PlotTaskDetail
            {
                TaskId = ct.Id,
                TaskName = ct.CultivationTaskName ?? ct.ProductionPlanTask.TaskName,
                Description = ct.Description ?? ct.ProductionPlanTask.Description,
                TaskType = ct.TaskType ?? ct.ProductionPlanTask.TaskType,
                Status = ct.Status,
                ExecutionOrder = ct.ExecutionOrder ?? 0,
                
                ScheduledEndDate = ct.ScheduledEndDate,
                ActualStartDate = ct.ActualStartDate,
                ActualEndDate = ct.ActualEndDate,
                
                ActualMaterialCost = ct.ActualMaterialCost,
                Materials = ct.ProductionPlanTask.ProductionPlanTaskMaterials.Select(pptm =>
                {
                    var actualMaterial = ct.CultivationTaskMaterials.FirstOrDefault(ctm => ctm.MaterialId == pptm.MaterialId);
                    return new TaskMaterialDetail
                    {
                        MaterialId = pptm.MaterialId,
                        MaterialName = pptm.Material.Name,
                        PlannedQuantity = (decimal)pptm.EstimatedAmount,
                        ActualQuantity = actualMaterial?.ActualQuantity,
                        ActualCost = actualMaterial?.ActualCost,
                        Unit = pptm.Material.Unit
                    };
                }).ToList()
            }).ToList();

            var response = new PlotImplementationResponse
            {
                PlotId = plot.Id,
                PlotName = $"{plot.SoThua}/{plot.SoTo}",
                SoThua = plot.SoThua+"" ?? "N/A",
                SoTo = plot.SoTo+"" ?? "N/A",
                PlotArea = plot.Area,
                
                FarmerId = plot.FarmerId,
                FarmerName = plot.Farmer?.FullName ?? "Unknown",
                
                ProductionPlanId = plan.Id,
                ProductionPlanName = plan.PlanName,
                
                SeasonName = plotCultivation?.Season?.SeasonName ?? "Unknown",
                RiceVarietyName = plotCultivation?.RiceVariety?.VarietyName ?? "Unknown",
                PlantingDate = plotCultivation?.PlantingDate ?? DateTime.MinValue,
                
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                InProgressTasks = inProgressTasks,
                PendingTasks = pendingTasks,
                CompletionPercentage = completionPercentage,
                
                Tasks = taskDetails
            };

            _logger.LogInformation("Successfully retrieved plot implementation for Plot {PlotId} and Plan {PlanId}", request.PlotId, request.ProductionPlanId);

            return Result<PlotImplementationResponse>.Success(response, "Successfully retrieved plot implementation details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving plot implementation for Plot {PlotId} and Plan {PlanId}", request.PlotId, request.ProductionPlanId);
            return Result<PlotImplementationResponse>.Failure("An error occurred while retrieving plot implementation.", "GetPlotImplementationFailed");
        }
    }
}

