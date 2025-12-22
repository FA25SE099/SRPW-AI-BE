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

            // Get PlotCultivation IDs and their latest versions
            var plotCultivationData = allPlotsInGroup
                .SelectMany(p => p.PlotCultivations)
                .Select(pc => new
                {
                    PlotCultivationId = pc.Id,
                    PlotId = pc.PlotId
                })
                .ToList();

            var plotCultivationIds = plotCultivationData.Select(pc => pc.PlotCultivationId).ToList();

            // Get the latest version for each PlotCultivation
            var latestVersions = await _unitOfWork.Repository<CultivationVersion>()
                .ListAsync(
                    filter: v => plotCultivationIds.Contains(v.PlotCultivationId),
                    orderBy: q => q.OrderByDescending(v => v.VersionOrder)
                );

            // Group by PlotCultivationId and take the latest version (highest VersionOrder)
            var latestVersionMap = latestVersions
                .GroupBy(v => v.PlotCultivationId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(v => v.VersionOrder).First()
                );

            _logger.LogInformation(
                "Found {PlotCultivationCount} plot cultivations with {VersionCount} versions. Latest versions mapped: {MappedCount}",
                plotCultivationIds.Count, latestVersions.Count, latestVersionMap.Count);

            // 2. Filter active UAV Order Assignments (check by specific task)
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

            // 3. Load CultivationTasks ONLY from latest versions
            var latestVersionIds = latestVersionMap.Values.Select(v => v.Id).ToList();
            
            Expression<Func<CultivationTask, bool>> taskFilter = ct =>
                plotCultivationIds.Contains(ct.PlotCultivationId) && 
                ct.VersionId.HasValue &&
                latestVersionIds.Contains(ct.VersionId.Value) && // ✅ Only latest versions
                (ct.TaskType == request.RequiredTaskType);

            var potentialTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: taskFilter,
                includeProperties: q => q
                    .Include(ct => ct.PlotCultivation).ThenInclude(pc => pc.Plot)
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
                    .Include(ct => ct.Version) // Include version for logging
            );

            _logger.LogInformation(
                "Loaded {TaskCount} potential tasks from latest versions (Type: {TaskType})",
                potentialTasks.Count, request.RequiredTaskType);
            
            // 4. Build response for ALL plots in the group
            var plotResponses = new List<UavPlotReadinessResponse>();

            foreach (var plot in allPlotsInGroup)
            {
                var plotCultivation = plot.PlotCultivations.FirstOrDefault();
                var plotArea = plotCultivation?.Area ?? 0M;
                var hasActiveUavOrder = busyPlotIdSet.Contains(plot.Id);
                
                // Get the latest version for this plot's cultivation
                Guid? latestVersionId = null;
                string? latestVersionName = null;
                if (plotCultivation != null && latestVersionMap.TryGetValue(plotCultivation.Id, out var latestVersion))
                {
                    latestVersionId = latestVersion.Id;
                    latestVersionName = latestVersion.VersionName;
                }
                
                // Find tasks from the latest version only
                var relevantTasks = potentialTasks
                    .Where(t => t.PlotCultivation.PlotId == plot.Id)
                    .Where(t => t.Status != RiceProduction.Domain.Enums.TaskStatus.Completed && 
                               t.Status != RiceProduction.Domain.Enums.TaskStatus.Cancelled)
                    .Where(t => t.ProductionPlanTask != null) // Must have ProductionPlanTask for scheduling
                    .OrderBy(t => t.ProductionPlanTask.ScheduledDate)
                    .ThenBy(t => t.ExecutionOrder ?? 0) // Then by execution order
                    .ToList();

                if (relevantTasks.Any())
                {
                    // Group by unique task identifier to avoid duplicates
                    var uniqueTasks = relevantTasks
                        .GroupBy(t => t.Id)
                        .Select(g => g.First())
                        .ToList();

                    _logger.LogInformation(
                        "Plot {PlotId} ({PlotName}): Found {TaskCount} tasks in version {VersionName}",
                        plot.Id, $"Thửa {plot.SoThua}, Tờ {plot.SoTo}", 
                        uniqueTasks.Count, latestVersionName ?? "Unknown");

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
                            : $"No pending {request.RequiredTaskType} tasks in latest version ({latestVersionName ?? "Unknown"})",
                        HasActiveUavOrder = hasActiveUavOrder
                    });
                    
                    _logger.LogInformation(
                        "Plot {PlotId} ({PlotName}): No ready tasks found in version {VersionName}",
                        plot.Id, $"Thửa {plot.SoThua}, Tờ {plot.SoTo}", latestVersionName ?? "Unknown");
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