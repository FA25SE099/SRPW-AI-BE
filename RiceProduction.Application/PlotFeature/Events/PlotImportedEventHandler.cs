using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.PlotFeature.Events;

public class PlotImportedEventHandler : INotificationHandler<PlotImportedEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlotImportedEventHandler> _logger;

    public PlotImportedEventHandler(
        IUnitOfWork unitOfWork,
        ILogger<PlotImportedEventHandler> logger)
    {
        _unitOfWork = unitOfWork;
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
        var plots = await _unitOfWork.Repository<Plot>()
            .ListAsync(p => plotIds.Contains(p.Id));

        var polygonTaskRepo = _unitOfWork.Repository<PlotPolygonTask>();
        var notificationRepo = _unitOfWork.Repository<Notification>();

        var existingTasks = await polygonTaskRepo
            .ListAsync(t => plotIds.Contains(t.PlotId) &&
                           (t.Status == "Pending" || t.Status == "InProgress"));
        var plotsWithExistingTasks = existingTasks.Select(t => t.PlotId).ToHashSet();

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

            await polygonTaskRepo.AddAsync(polygonTask);

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

            await notificationRepo.AddAsync(supervisorNotification);

            supervisorIndex++;
            tasksCreated++;
        }

        await _unitOfWork.CompleteAsync();

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
        ClusterManager? clusterManager = null;
        if (clusterManagerId.HasValue)
        {
            clusterManager = await _unitOfWork.ClusterManagerRepository
                .GetClusterManagerByIdAsync(clusterManagerId.Value, cancellationToken);
        }

        if (clusterManager?.ClusterId == null)
        {
            _logger.LogInformation("No cluster context available, searching for supervisors system-wide");
            return await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.CurrentFarmerCount < s.MaxFarmerCapacity);
        }

        var clusterId = clusterManager.ClusterId;

        // Strategy 1: Supervisors directly assigned to the cluster
        _logger.LogInformation(
            "Attempting to find supervisors for cluster {ClusterId} using direct cluster relationship",
            clusterId);

        var directSupervisors = await _unitOfWork.SupervisorRepository
            .ListAsync(s => s.ClusterId == clusterId);

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

        var groups = await _unitOfWork.Repository<Group>()
            .ListAsync(g => g.ClusterId == clusterId && g.SupervisorId != null);
        var supervisorIds = groups.Select(g => g.SupervisorId!.Value).Distinct().ToList();

        if (supervisorIds.Any())
        {
            var groupSupervisors = await _unitOfWork.SupervisorRepository
                .ListAsync(s => supervisorIds.Contains(s.Id) &&
                               s.CurrentFarmerCount < s.MaxFarmerCapacity);

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

        var systemSupervisors = await _unitOfWork.SupervisorRepository
            .ListAsync(s => s.CurrentFarmerCount < s.MaxFarmerCapacity);

        if (systemSupervisors.Any())
        {
            _logger.LogInformation(
                "Found {SupervisorCount} available supervisors system-wide (not cluster-specific)",
                systemSupervisors.Count);
        }

        return systemSupervisors;
    }
}
