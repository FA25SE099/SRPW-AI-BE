using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Query; // Cần thiết cho Includes

namespace RiceProduction.Application.UAVFeature.Queries.GetPlotsReadyForUav;

public class GetPlotsReadyForUavQueryHandler : IRequestHandler<GetPlotsReadyForUavQuery, Result<List<UavPlotReadinessResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlotsReadyForUavQueryHandler> _logger;

    public GetPlotsReadyForUavQueryHandler(IUnitOfWork unitOfWork, ILogger<GetPlotsReadyForUavQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<UavPlotReadinessResponse>>> Handle(GetPlotsReadyForUavQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var todayUtc = DateTime.UtcNow.Date;
            
            // 1. Tải Group và các Plots trực tiếp của nó
            var group = await _unitOfWork.Repository<Group>()
                .FindAsync(
                    match: g => g.Id == request.GroupId,
                    includeProperties: q => q.Include(g => g.GroupPlots).ThenInclude(gp => gp.Plot).ThenInclude(p => p.PlotCultivations)
                );

            if (group == null)
            {
                return Result<List<UavPlotReadinessResponse>>.Failure("Group not found.", "GroupNotFound");
            }
            
            var allPlotsInGroup = group.GroupPlots.Select(gp => gp.Plot).ToList();
            
            if (!allPlotsInGroup.Any())
            {
                return Result<List<UavPlotReadinessResponse>>.Success(new List<UavPlotReadinessResponse>(), $"No plots found in Group {request.GroupId}.");
            }
            
            var allPlotIdsInGroup = allPlotsInGroup.Select(p => p.Id).ToList();

            // Lấy PlotCultivation IDs đang hoạt động trong Group này
            var plotCultivationIdSet = allPlotsInGroup
                .SelectMany(p => p.PlotCultivations)
                .Select(pc => pc.Id)
                .ToHashSet();

            // 2. Lọc ra các CultivationTask đang có Active UAV Order Assignment (kiểm tra theo task cụ thể)
            var busyAssignments = await _unitOfWork.Repository<UavOrderPlotAssignment>()
                .ListAsync(
                    filter: a => 
                        allPlotIdsInGroup.Contains(a.PlotId) && 
                        (a.Status == RiceProduction.Domain.Enums.TaskStatus.Draft || 
                         a.Status == RiceProduction.Domain.Enums.TaskStatus.PendingApproval || 
                         a.Status == RiceProduction.Domain.Enums.TaskStatus.InProgress)
                );
            // Track which specific cultivation tasks are already assigned (not just plots)
            var busyTaskIdSet = busyAssignments
                .Select(a => a.CultivationTaskId)
                .ToHashSet();
            
            // Also track plots with active orders (for informational purposes)
            var busyPlotIdSet = busyAssignments.Select(a => a.PlotId).Distinct().ToHashSet();

            // 3. Tải tất cả CultivationTasks tiềm năng và các mối quan hệ liên quan
            Expression<Func<CultivationTask, bool>> taskFilter = ct =>
                plotCultivationIdSet.Contains(ct.PlotCultivationId) && 
                (ct.TaskType == request.RequiredTaskType);

            var potentialTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: taskFilter,
                includeProperties: q => q
                    .Include(ct => ct.PlotCultivation).ThenInclude(pc => pc.Plot)
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
            );

            // 4. Build response for ALL plots in the group
            var plotResponses = new List<UavPlotReadinessResponse>();

            foreach (var plot in allPlotsInGroup)
            {
                var plotCultivation = plot.PlotCultivations.FirstOrDefault();
                var plotArea = plotCultivation?.Area ?? 0M;
                var hasActiveUavOrder = busyPlotIdSet.Contains(plot.Id);
                
                // Find the most relevant task for this plot
                var relevantTasks = potentialTasks
                    .Where(t => t.PlotCultivation.PlotId == plot.Id)
                    .Where(t => t.Status != RiceProduction.Domain.Enums.TaskStatus.Completed && 
                               t.Status != RiceProduction.Domain.Enums.TaskStatus.Cancelled)
                    .OrderBy(t => t.ProductionPlanTask.ScheduledDate)
                    .ToList();

                if (relevantTasks.Any())
                {
                    // Group by unique task identifier to avoid duplicates
                    var uniqueTasks = relevantTasks
                        .GroupBy(t => new { 
                            t.Id,
                            t.PlotCultivationId, 
                            ScheduledDate = t.ProductionPlanTask.ScheduledDate.Date,
                            t.CultivationTaskName,
                            t.TaskType
                        })
                        .Select(g => g.First())
                        .ToList();

                    foreach (var task in uniqueTasks)
                    {
                        var scheduledDate = task.ProductionPlanTask.ScheduledDate.Date;
                        var isScheduledSoon = scheduledDate <= todayUtc.AddDays(request.DaysBeforeScheduled);
                        
                        // Check if THIS SPECIFIC TASK has an active UAV order (not just the plot)
                        var hasActiveTaskOrder = busyTaskIdSet.Contains(task.Id);
                        
                        // Determine readiness status - task is ready if scheduled soon AND not already assigned
                        bool isReady = isScheduledSoon && !hasActiveTaskOrder;
                        string readyStatus = DetermineReadyStatus(isScheduledSoon, hasActiveTaskOrder, scheduledDate, todayUtc, hasActiveUavOrder);

                        var estimatedMaterialCost = task.ProductionPlanTask.ProductionPlanTaskMaterials
                            .Sum(pptm => pptm.EstimatedAmount.GetValueOrDefault(0M));
                        
                        plotResponses.Add(new UavPlotReadinessResponse
                        {
                            PlotId = plot.Id,
                            PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                            PlotCultivationId = task.PlotCultivationId,
                            CultivationTaskId = task.Id,
                            PlotArea = plotArea,
                            ReadyDate = scheduledDate,
                            TaskType = task.TaskType,
                            CultivationTaskName = task.CultivationTaskName ?? task.ProductionPlanTask.TaskName,
                            EstimatedMaterialCost = estimatedMaterialCost,
                            IsReady = isReady,
                            ReadyStatus = readyStatus,
                            HasActiveUavOrder = hasActiveTaskOrder // This task specifically has an order
                        });
                    }
                }
                else
                {
                    // Plot has no relevant tasks - add it anyway with "Not Ready" status
                    plotResponses.Add(new UavPlotReadinessResponse
                    {
                        PlotId = plot.Id,
                        PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                        PlotCultivationId = plotCultivation?.Id,
                        CultivationTaskId = null,
                        PlotArea = plotArea,
                        ReadyDate = null,
                        TaskType = null,
                        CultivationTaskName = string.Empty,
                        EstimatedMaterialCost = 0,
                        IsReady = false,
                        ReadyStatus = hasActiveUavOrder 
                            ? "Plot has active UAV order, no pending tasks" 
                            : $"No pending {request.RequiredTaskType} tasks scheduled",
                        HasActiveUavOrder = hasActiveUavOrder
                    });
                }
            }

            _logger.LogInformation("Retrieved {TotalCount} plot records from Group {GroupId}. {ReadyCount} plots are ready for UAV service.", 
                plotResponses.Count, request.GroupId, plotResponses.Count(p => p.IsReady));

            return Result<List<UavPlotReadinessResponse>>.Success(plotResponses, $"Successfully retrieved {plotResponses.Count} plots from group.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ready plots for UAV in Group {GroupId}", request.GroupId);
            return Result<List<UavPlotReadinessResponse>>.Failure("Failed to retrieve ready plots.", "GetReadyPlotsFailed");
        }
    }

    private string DetermineReadyStatus(bool isScheduledSoon, bool hasActiveTaskOrder, DateTime scheduledDate, DateTime todayUtc, bool plotHasAnyActiveOrder)
    {
        if (hasActiveTaskOrder)
        {
            return "This task already has an active UAV order";
        }

        if (!isScheduledSoon)
        {
            var daysUntilScheduled = (scheduledDate - todayUtc).Days;
            return $"Task scheduled in {daysUntilScheduled} days - not yet ready";
        }

        if (scheduledDate < todayUtc)
        {
            var daysOverdue = (todayUtc - scheduledDate).Days;
            if (plotHasAnyActiveOrder)
            {
                return $"Ready - Task overdue by {daysOverdue} days (plot has other active orders)";
            }
            return $"Ready - Task overdue by {daysOverdue} days";
        }

        if (scheduledDate == todayUtc)
        {
            if (plotHasAnyActiveOrder)
            {
                return "Ready - Task scheduled for today (plot has other active orders)";
            }
            return "Ready - Task scheduled for today";
        }

        var daysUntil = (scheduledDate - todayUtc).Days;
        if (plotHasAnyActiveOrder)
        {
            return $"Ready - Task scheduled in {daysUntil} days (plot has other active orders)";
        }
        return $"Ready - Task scheduled in {daysUntil} days";
    }
}