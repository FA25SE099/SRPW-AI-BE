using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.PlotFeature.Events;

public class PlotImportedEventHandler : INotificationHandler<PlotImportedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PlotImportedEventHandler> _logger;

    public PlotImportedEventHandler(
        IApplicationDbContext context,
        ILogger<PlotImportedEventHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(PlotImportedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing plot import event to create polygon assignment tasks for {PlotCount} plots",
            notification.TotalPlotsImported);

        if (!notification.ImportedPlots.Any())
        {
            _logger.LogInformation("No plots imported, skipping task assignment");
            return;
        }

        var availableSupervisors = await ResolveAvailableSupervisorsAsync(
            notification.ClusterManagerId,
            cancellationToken);

        var supervisorList = availableSupervisors.OrderBy(s => s.CurrentFarmerCount).ToList();
        if (!supervisorList.Any())
        {
            _logger.LogWarning("No available supervisors found for polygon task assignment");
            return;
        }

        var plotIds = notification.ImportedPlots.Select(p => p.PlotId).ToList();
        var plots = await _context.Plots
            .AsNoTracking()
            .Where(p => plotIds.Contains(p.Id))
            .Select(p => new { p.Id, p.SoThua, p.SoTo, p.Area })
            .ToListAsync(cancellationToken);

        var plotsWithExistingTasks = (await _context.PlotPolygonTasks
            .AsNoTracking()
            .Where(t => plotIds.Contains(t.PlotId) &&
                        (t.Status == "Pending" || t.Status == "InProgress"))
            .Select(t => t.PlotId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        _logger.LogInformation(
            "Found {ExistingTaskCount} plots with existing polygon tasks, will skip assignment",
            plotsWithExistingTasks.Count);

        var supervisorIndex = 0;
        var tasksCreated = 0;
        var skipped = 0;

        foreach (var plot in plots)
        {
            if (plotsWithExistingTasks.Contains(plot.Id))
            {
                _logger.LogDebug(
                    "Skipping plot {PlotId} (SoThua: {SoThua}, SoTo: {SoTo}) - already has a pending/in-progress polygon task",
                    plot.Id, plot.SoThua, plot.SoTo);
                skipped++;
                continue;
            }

            var supervisor = supervisorList[supervisorIndex % supervisorList.Count];

            var polygonTask = new PlotPolygonTask
            {
                PlotId = plot.Id,
                AssignedToSupervisorId = supervisor.Id,
                AssignedByClusterManagerId = notification.ClusterManagerId,
                Status = "Pending",
                AssignedAt = DateTime.UtcNow,
                Priority = 1,
                Notes = $"Auto-assigned: Draw polygon boundary for Plot - SoThua: {plot.SoThua}, SoTo: {plot.SoTo}, Area: {plot.Area} ha (imported via Excel)"
            };

            _context.PlotPolygonTasks.Add(polygonTask);

            var supervisorNotification = new Notification
            {
                RecipientId = supervisor.Id.ToString(),
                ActivityType = "polygon_task_assigned",
                ObjectType = "plot_polygon_task",
                TimeSent = DateTime.UtcNow,
                IsUnread = true,
                Content = $"New Task: Draw polygon boundary for Plot {plot.SoThua}/{plot.SoTo} ({plot.Area} ha). This plot was imported via Excel.",
                Status = "pending"
            };

            _context.Notifications.Add(supervisorNotification);

            supervisorIndex++;
            tasksCreated++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created {TaskCount} polygon assignment tasks distributed across {SupervisorCount} supervisors ({SkippedCount} plots skipped - already assigned)",
            tasksCreated,
            supervisorList.Count,
            skipped);
    }

    private async Task<IReadOnlyList<Supervisor>> ResolveAvailableSupervisorsAsync(
        Guid? clusterManagerId,
        CancellationToken cancellationToken)
    {
        Guid? clusterId = null;
        if (clusterManagerId.HasValue)
        {
            clusterId = await _context.ClusterManagers
                .AsNoTracking()
                .Where(cm => cm.Id == clusterManagerId.Value)
                .Select(cm => cm.ClusterId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (clusterId == null)
        {
            _logger.LogInformation("No cluster context available, searching for supervisors system-wide");
            return await _context.Supervisors
                .AsNoTracking()
                .Where(s => s.CurrentFarmerCount < s.MaxFarmerCapacity)
                .ToListAsync(cancellationToken);
        }

        // Strategy 1: Supervisors directly assigned to the cluster
        _logger.LogInformation(
            "Attempting to find supervisors for cluster {ClusterId} using direct cluster relationship",
            clusterId);

        var directSupervisors = await _context.Supervisors
            .AsNoTracking()
            .Where(s => s.ClusterId == clusterId)
            .ToListAsync(cancellationToken);

        if (directSupervisors.Any())
        {
            _logger.LogInformation(
                "Found {SupervisorCount} available supervisors directly assigned to cluster {ClusterId}",
                directSupervisors.Count, clusterId);
            return directSupervisors;
        }

        // Strategy 2: Supervisors managing groups in this cluster with available capacity
        _logger.LogWarning(
            "No supervisors directly assigned to cluster {ClusterId}, trying group-based lookup",
            clusterId);

        var supervisorIds = await _context.Groups
            .AsNoTracking()
            .Where(g => g.ClusterId == clusterId && g.SupervisorId != null)
            .Select(g => g.SupervisorId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (supervisorIds.Any())
        {
            var groupSupervisors = await _context.Supervisors
                .AsNoTracking()
                .Where(s => supervisorIds.Contains(s.Id) &&
                            s.CurrentFarmerCount < s.MaxFarmerCapacity)
                .ToListAsync(cancellationToken);

            if (groupSupervisors.Any())
            {
                _logger.LogInformation(
                    "Found {SupervisorCount} available supervisors via group assignments in cluster {ClusterId}",
                    groupSupervisors.Count, clusterId);
                return groupSupervisors;
            }

            _logger.LogWarning(
                "Found supervisors in cluster {ClusterId} via groups, but all are at capacity",
                clusterId);
        }
        else
        {
            _logger.LogWarning(
                "No supervisors found in groups for cluster {ClusterId}",
                clusterId);
        }

        // Strategy 3: Fallback to any available supervisor system-wide
        _logger.LogWarning(
            "No cluster-specific supervisors available for cluster {ClusterId}, falling back to system-wide search",
            clusterId);

        var systemSupervisors = await _context.Supervisors
            .AsNoTracking()
            .Where(s => s.CurrentFarmerCount < s.MaxFarmerCapacity)
            .ToListAsync(cancellationToken);

        if (systemSupervisors.Any())
        {
            _logger.LogInformation(
                "Found {SupervisorCount} available supervisors system-wide (not cluster-specific)",
                systemSupervisors.Count);
        }

        return systemSupervisors;
    }
}
