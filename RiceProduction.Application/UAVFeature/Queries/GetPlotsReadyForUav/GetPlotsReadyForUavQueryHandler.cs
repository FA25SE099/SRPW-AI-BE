using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.UAVFeature.Queries.GetPlotsReadyForUav;

public class GetPlotsReadyForUavQueryHandler : IRequestHandler<GetPlotsReadyForUavQuery, Result<List<UavPlotReadinessResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPlotsReadyForUavQueryHandler> _logger;

    public GetPlotsReadyForUavQueryHandler(IApplicationDbContext context, ILogger<GetPlotsReadyForUavQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<List<UavPlotReadinessResponse>>> Handle(GetPlotsReadyForUavQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var todayUtc = DateTime.UtcNow.Date;
            
            // 1. Load Group and its Plots without PostGIS geometry columns
            var groupData = await _context.Groups
                .AsNoTracking()
                .Where(g => g.Id == request.GroupId)
                .Select(g => new
                {
                    g.Id,
                    Plots = g.GroupPlots.Select(gp => new
                    {
                        gp.Plot.Id,
                        gp.Plot.SoThua,
                        gp.Plot.SoTo,
                        PlotCultivations = gp.Plot.PlotCultivations
                            .Select(pc => new { pc.Id, pc.Area })
                            .ToList()
                    }).ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (groupData == null)
            {
                return Result<List<UavPlotReadinessResponse>>.Failure("Group not found.", "GroupNotFound");
            }
            
            var allPlotsInGroup = groupData.Plots;
            
            if (allPlotsInGroup.Count == 0)
            {
                return Result<List<UavPlotReadinessResponse>>.Success(new List<UavPlotReadinessResponse>(), $"No plots found in Group {request.GroupId}.");
            }
            
            var allPlotIdsInGroup = allPlotsInGroup.Select(p => p.Id).ToList();

            var plotCultivationIds = allPlotsInGroup
                .SelectMany(p => p.PlotCultivations)
                .Select(pc => pc.Id)
                .ToList();

            var latestVersions = await _context.CultivationVersions
                .AsNoTracking()
                .Where(v => plotCultivationIds.Contains(v.PlotCultivationId))
                .OrderByDescending(v => v.VersionOrder)
                .ToListAsync(cancellationToken);

            var latestVersionMap = latestVersions
                .GroupBy(v => v.PlotCultivationId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(v => v.VersionOrder).First());

            _logger.LogInformation(
                "Found {PlotCultivationCount} plot cultivations with {VersionCount} versions. Latest versions mapped: {MappedCount}",
                plotCultivationIds.Count, latestVersions.Count, latestVersionMap.Count);

            var busyAssignments = await _context.UavOrderPlotAssignments
                .AsNoTracking()
                .Where(a =>
                    allPlotIdsInGroup.Contains(a.PlotId) &&
                    (a.Status == TaskStatus.Draft ||
                     a.Status == TaskStatus.PendingApproval ||
                     a.Status == TaskStatus.InProgress))
                .Select(a => new { a.CultivationTaskId, a.PlotId })
                .ToListAsync(cancellationToken);

            // Track both specific tasks and plots with active orders
            var busyTaskIdSet = busyAssignments.Select(a => a.CultivationTaskId).ToHashSet();
            var busyPlotIdSet = busyAssignments.Select(a => a.PlotId).ToHashSet();

            var latestVersionIds = latestVersionMap.Values.Select(v => v.Id).ToList();

            var potentialTasks = await _context.CultivationTasks
                .AsNoTracking()
                .Include(ct => ct.PlotCultivation)
                .Include(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt!.ProductionPlanTaskMaterials)
                .Include(ct => ct.Version)
                .Where(ct =>
                    plotCultivationIds.Contains(ct.PlotCultivationId) &&
                    ct.VersionId.HasValue &&
                    latestVersionIds.Contains(ct.VersionId.Value) &&
                    ct.TaskType == request.RequiredTaskType)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Loaded {TaskCount} potential tasks from latest versions (Type: {TaskType})",
                potentialTasks.Count, request.RequiredTaskType);

            var plotResponses = new List<UavPlotReadinessResponse>();

            foreach (var plot in allPlotsInGroup)
            {
                var plotCultivation = plot.PlotCultivations.FirstOrDefault();
                var plotArea = plotCultivation?.Area ?? 0M;
                var hasActiveUavOrder = busyPlotIdSet.Contains(plot.Id);
                var plotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}";
                var logPlotName = $"Thửa {plot.SoThua}, Tờ {plot.SoTo}";

                string? latestVersionName = plotCultivation != null &&
                    latestVersionMap.TryGetValue(plotCultivation.Id, out var latestVersion)
                    ? latestVersion.VersionName
                    : null;

                var uniqueTasks = potentialTasks
                    .Where(t => t.PlotCultivation.PlotId == plot.Id &&
                                t.Status != TaskStatus.Completed &&
                                t.Status != TaskStatus.Cancelled &&
                                t.ProductionPlanTask != null)
                    .OrderBy(t => t.ProductionPlanTask!.ScheduledDate)
                    .ThenBy(t => t.ExecutionOrder ?? 0)
                    .GroupBy(t => t.Id)
                    .Select(g => g.First())
                    .ToList();

                if (uniqueTasks.Count == 0)
                {
                    plotResponses.Add(new UavPlotReadinessResponse
                    {
                        PlotId = plot.Id,
                        PlotName = plotName,
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
                        plot.Id, logPlotName, latestVersionName ?? "Unknown");
                    continue;
                }

                _logger.LogInformation(
                    "Plot {PlotId} ({PlotName}): Found {TaskCount} tasks in version {VersionName}",
                    plot.Id, logPlotName, uniqueTasks.Count, latestVersionName ?? "Unknown");

                foreach (var task in uniqueTasks)
                {
                    var scheduledDate = task.ProductionPlanTask!.ScheduledDate.Date;
                    var isScheduledSoon = scheduledDate <= todayUtc.AddDays(request.DaysBeforeScheduled);
                    var hasActiveTaskOrder = busyTaskIdSet.Contains(task.Id);
                    var estimatedMaterialCost = task.ProductionPlanTask.ProductionPlanTaskMaterials
                        .Sum(pptm => pptm.EstimatedAmount.GetValueOrDefault(0M));

                    plotResponses.Add(new UavPlotReadinessResponse
                    {
                        PlotId = plot.Id,
                        PlotName = plotName,
                        PlotCultivationId = task.PlotCultivationId,
                        CultivationTaskId = task.Id,
                        PlotArea = plotArea,
                        ReadyDate = scheduledDate,
                        TaskType = task.TaskType,
                        CultivationTaskName = task.CultivationTaskName ?? task.ProductionPlanTask.TaskName,
                        EstimatedMaterialCost = estimatedMaterialCost,
                        IsReady = isScheduledSoon && !hasActiveTaskOrder,
                        ReadyStatus = DetermineReadyStatus(isScheduledSoon, hasActiveTaskOrder, scheduledDate, todayUtc, hasActiveUavOrder),
                        HasActiveUavOrder = hasActiveTaskOrder
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

    private static string DetermineReadyStatus(
        bool isScheduledSoon,
        bool hasActiveTaskOrder,
        DateTime scheduledDate,
        DateTime todayUtc,
        bool plotHasAnyActiveOrder)
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

        var suffix = plotHasAnyActiveOrder ? " (plot has other active orders)" : string.Empty;

        if (scheduledDate < todayUtc)
        {
            var daysOverdue = (todayUtc - scheduledDate).Days;
            return $"Ready - Task overdue by {daysOverdue} days{suffix}";
        }

        if (scheduledDate == todayUtc)
        {
            return $"Ready - Task scheduled for today{suffix}";
        }

        var daysUntil = (scheduledDate - todayUtc).Days;
        return $"Ready - Task scheduled in {daysUntil} days{suffix}";
    }
}
