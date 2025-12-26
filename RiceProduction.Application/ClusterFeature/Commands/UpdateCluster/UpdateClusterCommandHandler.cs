using Microsoft.Extensions.Logging;
using RiceProduction.Application.ClusterFeature.Commands.CreateCluster;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterFeature.Commands.UpdateCluster
{
    public class UpdateClusterCommandHandler : IRequestHandler<UpdateClusterCommand, Result<Guid>>
    {
        private readonly IClusterRepository _clusterRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateClusterCommandHandler> _logger;

        public UpdateClusterCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateClusterCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _clusterRepo = _unitOfWork.ClusterRepository;
            _logger = logger;
        }
        public async Task<Result<Guid>> Handle(UpdateClusterCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var cluster = await _clusterRepo.GetClusterByIdAsync(
                    request.ClusterId,
                    cancellationToken);
                if (cluster == null)
                {
                    return Result<Guid>.Failure("Cluster not found");
                }

                var oldClusterManagerId = cluster.ClusterManagerId;
                var oldAgronomyExpertId = cluster.AgronomyExpertId;

                cluster.ClusterName = request.ClusterName;
                cluster.ClusterManagerId = request.ClusterManagerId;
                cluster.AgronomyExpertId = request.AgronomyExpertId;

                var clusterRepo = _unitOfWork.Repository<Cluster>();

                var duplicateName = await clusterRepo.FindAsync(m => m.ClusterName == request.ClusterName);
                if (duplicateName != null)
                {
                    if (duplicateName.Id != cluster.Id)
                    {
                        return Result<Guid>.Failure($"Cluster with name '{request.ClusterName}' already exists");
                    }
                }
                var duplicateMan = await clusterRepo.FindAsync(m => m.ClusterManagerId == request.ClusterManagerId);
                if (duplicateMan != null)
                {
                    if (duplicateMan.Id != cluster.Id)
                    {
                        return Result<Guid>.Failure($"Cluster manager ID = '{request.ClusterManagerId}' already managing another cluster with ID = '{duplicateMan.Id}'");
                    }
                }
                var duplicateExpert = await clusterRepo.FindAsync(m => m.AgronomyExpertId == request.AgronomyExpertId);
                if (duplicateExpert != null)
                {
                    if (duplicateExpert.Id != cluster.Id)
                    {
                        return Result<Guid>.Failure($"Agronomy Expert ID = '{request.AgronomyExpertId}' already managing another cluster with ID = '{duplicateExpert.Id}'");
                    }
                }

                // Handle ClusterManager changes
                if (oldClusterManagerId != request.ClusterManagerId)
                {
                    // Remove old cluster manager assignment
                    if (oldClusterManagerId.HasValue)
                    {
                        var oldManager = await _unitOfWork.ClusterManagerRepository.GetEntityByIdAsync(c => c.Id == oldClusterManagerId.Value);
                        if (oldManager != null)
                        {
                            oldManager.ClusterId = null;
                            oldManager.AssignedDate = null;
                            _unitOfWork.ClusterManagerRepository.Update(oldManager);
                        }
                    }

                    // Assign new cluster manager
                    if (request.ClusterManagerId.HasValue)
                    {
                        var newManager = await _unitOfWork.ClusterManagerRepository.GetEntityByIdAsync(c => c.Id == request.ClusterManagerId.Value);
                        if (newManager != null)
                        {
                            newManager.ClusterId = cluster.Id;
                            newManager.AssignedDate = DateTime.UtcNow;
                            _unitOfWork.ClusterManagerRepository.Update(newManager);
                        }
                        else
                        {
                            _logger.LogWarning("Cannot find cluster manager with id: {ClusterManagerId}", request.ClusterManagerId);
                        }
                    }
                }

                // Handle AgronomyExpert changes
                if (oldAgronomyExpertId != request.AgronomyExpertId)
                {
                    // Remove old agronomy expert assignment
                    if (oldAgronomyExpertId.HasValue)
                    {
                        var oldExpert = await _unitOfWork.AgronomyExpertRepository.GetAgronomyExpertByIdAsync(oldAgronomyExpertId.Value, cancellationToken);
                        if (oldExpert != null)
                        {
                            oldExpert.ClusterId = null;
                            oldExpert.AssignedDate = null;
                            _unitOfWork.AgronomyExpertRepository.Update(oldExpert);
                        }
                    }

                    // Assign new agronomy expert
                    if (request.AgronomyExpertId.HasValue)
                    {
                        var newExpert = await _unitOfWork.AgronomyExpertRepository.GetAgronomyExpertByIdAsync(request.AgronomyExpertId.Value, cancellationToken);
                        if (newExpert != null)
                        {
                            newExpert.ClusterId = cluster.Id;
                            newExpert.AssignedDate = DateTime.UtcNow;
                            _unitOfWork.AgronomyExpertRepository.Update(newExpert);
                        }
                        else
                        {
                            _logger.LogWarning("Cannot find agronomy expert with id: {AgronomyExpertId}", request.AgronomyExpertId);
                        }
                    }
                }

                // Handle Supervisor changes
                // Both null and empty list mean: remove all supervisors
                // Get current supervisors for this cluster
                var currentSupervisors = cluster.SupervisorsInCluster?.ToList() ?? new List<Supervisor>();
                var currentSupervisorIds = currentSupervisors.Select(s => s.Id).ToHashSet();
                var newSupervisorIds = request.SupervisorIds?.ToHashSet() ?? new HashSet<Guid>();

                // Remove supervisors that are no longer in the list
                var supervisorsToRemove = currentSupervisors.Where(s => !newSupervisorIds.Contains(s.Id)).ToList();
                foreach (var supervisor in supervisorsToRemove)
                {
                    supervisor.ClusterId = null;
                    supervisor.AssignedDate = null;
                    _unitOfWork.SupervisorRepository.Update(supervisor);
                    _logger.LogInformation("Removed supervisor {SupervisorId} from cluster {ClusterId}", 
                        supervisor.Id, cluster.Id);
                }

                // Add new supervisors (only if SupervisorIds is not null and has items)
                if (request.SupervisorIds != null && request.SupervisorIds.Count > 0)
                {
                    var supervisorIdsToAdd = newSupervisorIds.Where(id => !currentSupervisorIds.Contains(id)).ToList();
                    foreach (var supervisorId in supervisorIdsToAdd)
                    {
                        var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(supervisorId, cancellationToken);
                        if (supervisor != null)
                        {
                            // Check if supervisor is already assigned to another cluster
                            if (supervisor.ClusterId.HasValue && supervisor.ClusterId.Value != cluster.Id)
                            {
                                _logger.LogWarning("Supervisor {SupervisorId} is already assigned to cluster {ClusterId}", 
                                    supervisorId, supervisor.ClusterId);
                                continue;
                            }

                            supervisor.ClusterId = cluster.Id;
                            supervisor.AssignedDate = DateTime.UtcNow;
                            _unitOfWork.SupervisorRepository.Update(supervisor);
                            _logger.LogInformation("Assigned supervisor {SupervisorId} to cluster {ClusterId}", 
                                supervisorId, cluster.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Cannot find supervisor with id: {SupervisorId}", supervisorId);
                        }
                    }
                }

                var result = await _clusterRepo.UpdateCluster(request.ClusterId, cluster, cancellationToken);
                
                return Result<Guid>.Success(
                    cluster.Id,
                    "Cluster updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating cluster");
                return Result<Guid>.Failure("An error occurred while processing your request");
            }
        }
    }
}
