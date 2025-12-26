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

        ClusterManager? clusterManager = null;
        if (notification.ClusterManagerId.HasValue)
        {
            clusterManager = await _unitOfWork.ClusterManagerRepository
                .GetClusterManagerByIdAsync(notification.ClusterManagerId.Value, cancellationToken);
        }

        IEnumerable<Supervisor> availableSupervisors;
        
        // Strategy 1: Try to get supervisors directly from the cluster (best approach)
        if (clusterManager?.ClusterId != null)
        {
            _logger.LogInformation(
                "Attempting to find supervisors for cluster {ClusterId} using direct cluster relationship",
                clusterManager.ClusterId);

            // Get supervisors directly assigned to the cluster with available capacity
            availableSupervisors = await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.ClusterId == clusterManager.ClusterId);
            
            if (availableSupervisors.Any())
            {
                _logger.LogInformation(
                    "Found {SupervisorCount} available supervisors directly assigned to cluster {ClusterId}",
                    availableSupervisors.Count(), clusterManager.ClusterId);
            }
            else
            {
                // Strategy 2: Fallback to supervisors managing groups in this cluster
                _logger.LogWarning(
                    "No supervisors directly assigned to cluster {ClusterId}, trying group-based lookup",
                    clusterManager.ClusterId);
                
                var groups = await _unitOfWork.Repository<Group>()
                    .ListAsync(g => g.ClusterId == clusterManager.ClusterId && g.SupervisorId != null);
                
                var supervisorIds = groups.Select(g => g.SupervisorId!.Value).Distinct().ToList();
                
                if (supervisorIds.Any())
                {
                    availableSupervisors = await _unitOfWork.SupervisorRepository
                        .ListAsync(s => supervisorIds.Contains(s.Id) && 
                                       s.CurrentFarmerCount < s.MaxFarmerCapacity);
                    
                    if (availableSupervisors.Any())
                    {
                        _logger.LogInformation(
                            "Found {SupervisorCount} available supervisors via group assignments in cluster {ClusterId}",
                            availableSupervisors.Count(), clusterManager.ClusterId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Found supervisors in cluster {ClusterId} via groups, but all are at capacity",
                            clusterManager.ClusterId);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "No supervisors found in groups for cluster {ClusterId}",
                        clusterManager.ClusterId);
                }
                
                // Strategy 3: Last resort - get any available supervisor system-wide
                if (!availableSupervisors.Any())
                {
                    _logger.LogWarning(
                        "No cluster-specific supervisors available for cluster {ClusterId}, falling back to system-wide search",
                        clusterManager.ClusterId);
                    
                    availableSupervisors = await _unitOfWork.SupervisorRepository
                        .ListAsync(s => s.CurrentFarmerCount < s.MaxFarmerCapacity);
                    
                    if (availableSupervisors.Any())
                    {
                        _logger.LogInformation(
                            "Found {SupervisorCount} available supervisors system-wide (not cluster-specific)",
                            availableSupervisors.Count());
                    }
                }
            }
        }
        else
        {
            // No cluster context - use system-wide supervisor pool
            _logger.LogInformation("No cluster context available, searching for supervisors system-wide");
            
            availableSupervisors = await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.CurrentFarmerCount < s.MaxFarmerCapacity);
        }

        var supervisorList = availableSupervisors.OrderBy(s => s.CurrentFarmerCount).ToList();
        
        if (!supervisorList.Any())
        {
            _logger.LogWarning("No available supervisors found for polygon task assignment");
            return;
        }

        var plotIds = notification.ImportedPlots.Select(p => p.PlotId).ToList();
        var plots = await _unitOfWork.Repository<Plot>()
            .ListAsync(p => plotIds.Contains(p.Id));

        // Check for existing pending or in-progress tasks for these plots
        var existingTasks = await _unitOfWork.Repository<PlotPolygonTask>()
            .ListAsync(t => plotIds.Contains(t.PlotId) && 
                           (t.Status == "Pending" || t.Status == "InProgress"));
        var plotsWithExistingTasks = existingTasks.Select(t => t.PlotId).ToHashSet();

        _logger.LogInformation(
            "Found {ExistingTaskCount} plots with existing polygon tasks, will skip assignment",
            plotsWithExistingTasks.Count);

        int supervisorIndex = 0;
        int tasksCreated = 0;
        int skipped = 0;

        foreach (var plot in plots)
        {
            // Skip if plot already has a pending or in-progress task
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

            await _unitOfWork.Repository<PlotPolygonTask>().AddAsync(polygonTask);

            var notification_msg = new Notification
            {
                RecipientId = supervisor.Id.ToString(),
                ActivityType = "polygon_task_assigned",
                ObjectType = "plot_polygon_task",
                TimeSent = DateTime.UtcNow,
                IsUnread = true,
                Content = $"New Task: Draw polygon boundary for Plot {plot.SoThua}/{plot.SoTo} ({plot.Area} ha). This plot was imported via Excel.",
                Status = "pending"
            };

            await _unitOfWork.Repository<Notification>().AddAsync(notification_msg);

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
}

