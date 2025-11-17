//using MediatR;
//using Microsoft.Extensions.Logging;
//using RiceProduction.Application.Common.Interfaces;
//using RiceProduction.Domain.Entities;

//namespace RiceProduction.Application.FarmerFeature.Events;

//public class FarmerImportedEventHandler : INotificationHandler<FarmerImportedEvent>
//{
//    private readonly IUnitOfWork _unitOfWork;
//    private readonly ILogger<FarmerImportedEventHandler> _logger;

//    public FarmerImportedEventHandler(
//        IUnitOfWork unitOfWork,
//        ILogger<FarmerImportedEventHandler> logger)
//    {
//        _unitOfWork = unitOfWork;
//        _logger = logger;
//    }

//    public async Task Handle(FarmerImportedEvent notification, CancellationToken cancellationToken)
//    {
//        _logger.LogInformation("Processing farmer import event to create polygon assignment tasks");

//        if (!notification.ImportResult.CreatedPlotIds.Any())
//        {
//            _logger.LogInformation("No plots created during import, skipping task assignment");
//            return;
//        }

//        // Get cluster manager info to determine which supervisors to assign
//        ClusterManager? clusterManager = null;
//        if (notification.ClusterManagerId.HasValue)
//        {
//            clusterManager = await _unitOfWork.ClusterManagerRepository
//                .GetClusterManagerByIdAsync(notification.ClusterManagerId.Value, cancellationToken);
//        }

//        // Get available supervisors from the same cluster
//        IEnumerable<Supervisor> availableSupervisors;
        
//        if (clusterManager?.ClusterId != null)
//        {
//            // Get supervisors assigned to groups in this cluster
//            var groups = await _unitOfWork.Repository<Group>()
//                .ListAsync(g => g.ClusterId == clusterManager.ClusterId && g.SupervisorId != null);
            
//            var supervisorIds = groups.Select(g => g.SupervisorId!.Value).Distinct().ToList();
            
//            if (supervisorIds.Any())
//            {
//                availableSupervisors = await _unitOfWork.SupervisorRepository
//                    .ListAsync(s => supervisorIds.Contains(s.Id));
//            }
//            else
//            {
//                _logger.LogWarning("No supervisors found for cluster {ClusterId}", clusterManager.ClusterId);
//                return;
//            }
//        }
//        else
//        {
//            // Fallback: get all available supervisors with capacity
//            availableSupervisors = await _unitOfWork.SupervisorRepository
//                .ListAsync(s => s.CurrentFarmerCount < s.MaxFarmerCapacity);
//        }

//        var supervisorList = availableSupervisors.OrderBy(s => s.CurrentFarmerCount).ToList();
        
//        if (!supervisorList.Any())
//        {
//            _logger.LogWarning("No available supervisors found for polygon task assignment");
//            return;
//        }

//        // Get the plots that need polygon assignment
//        var plots = await _unitOfWork.Repository<Plot>()
//            .ListAsync(p => notification.ImportResult.CreatedPlotIds.Contains(p.Id) );

//        // Assign tasks in round-robin fashion
//        int supervisorIndex = 0;
//        int tasksCreated = 0;

//        foreach (var plot in plots)
//        {
//            var supervisor = supervisorList[supervisorIndex % supervisorList.Count];
            
//            // Create polygon assignment task
//            var polygonTask = new PlotPolygonTask
//            {
//                PlotId = plot.Id,
//                AssignedToSupervisorId = supervisor.Id,
//                AssignedByClusterManagerId = notification.ClusterManagerId,
//                Status = "Pending",
//                AssignedAt = DateTime.UtcNow,
//                Priority = 1,
//                Notes = $"Assign polygon boundary for Plot - SoThua: {plot.SoThua}, SoTo: {plot.SoTo}, Area: {plot.Area} ha"
//            };

//            await _unitOfWork.Repository<PlotPolygonTask>().AddAsync(polygonTask);

//            // Create notification for supervisor
//            var notification_msg = new Notification
//            {
//                RecipientId = supervisor.Id.ToString(),
//                ActivityType = "polygon_task_assigned",
//                ObjectType = "plot_polygon_task",
//                TimeSent = DateTime.UtcNow,
//                IsUnread = true,
//                Content = $"New Task: Assign polygon boundary for Plot {plot.SoThua}/{plot.SoTo} ({plot.Area} ha). Farmer will be assigned after polygon is set.",
//                Status = "pending"
//            };

//            await _unitOfWork.Repository<Notification>().AddAsync(notification_msg);

//            supervisorIndex++;
//            tasksCreated++;
//        }

//        await _unitOfWork.CompleteAsync();

//        _logger.LogInformation(
//            "Created {TaskCount} polygon assignment tasks distributed across {SupervisorCount} supervisors",
//            tasksCreated,
//            supervisorList.Count);
//    }
//}

