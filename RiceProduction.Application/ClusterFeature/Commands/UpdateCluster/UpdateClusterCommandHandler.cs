using Microsoft.Extensions.Logging;
using RiceProduction.Application.ClusterFeature.Commands.CreateCluster;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
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
                var cluster = await _clusterRepo.GetClusterByIdAsync(request.ClusterId, cancellationToken);
                if (cluster == null)
                {
                    return Result<Guid>.Failure("Cluster not found");
                }

                var oldClusterManagerId = cluster.ClusterManagerId;
                var oldAgronomyExpertId = cluster.AgronomyExpertId;

                cluster.ClusterName = request.ClusterName;
                cluster.ClusterManagerId = request.ClusterManagerId;
                cluster.AgronomyExpertId = request.AgronomyExpertId;

                var duplicateError = await ValidateUniquenessAsync(request, cluster.Id);
                if (duplicateError != null)
                {
                    return Result<Guid>.Failure(duplicateError);
                }

                await UpdateClusterManagerAssignmentAsync(cluster.Id, oldClusterManagerId, request.ClusterManagerId);
                await UpdateAgronomyExpertAssignmentAsync(cluster.Id, oldAgronomyExpertId, request.AgronomyExpertId, cancellationToken);
                await UpdateSupervisorAssignmentsAsync(cluster, request.SupervisorIds, cancellationToken);

                await _clusterRepo.UpdateCluster(request.ClusterId, cluster, cancellationToken);

                return Result<Guid>.Success(cluster.Id, "Cluster updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating cluster");
                return Result<Guid>.Failure("An error occurred while processing your request");
            }
        }

        private async Task<string?> ValidateUniquenessAsync(UpdateClusterCommand request, Guid clusterId)
        {
            var clusterRepo = _unitOfWork.Repository<Cluster>();

            var duplicateName = await clusterRepo.FindAsync(m => m.ClusterName == request.ClusterName);
            if (duplicateName != null && duplicateName.Id != clusterId)
            {
                return $"Cluster with name '{request.ClusterName}' already exists";
            }

            var duplicateMan = await clusterRepo.FindAsync(m => m.ClusterManagerId == request.ClusterManagerId);
            if (duplicateMan != null && duplicateMan.Id != clusterId)
            {
                return $"Cluster manager ID = '{request.ClusterManagerId}' already managing another cluster with ID = '{duplicateMan.Id}'";
            }

            var duplicateExpert = await clusterRepo.FindAsync(m => m.AgronomyExpertId == request.AgronomyExpertId);
            if (duplicateExpert != null && duplicateExpert.Id != clusterId)
            {
                return $"Agronomy Expert ID = '{request.AgronomyExpertId}' already managing another cluster with ID = '{duplicateExpert.Id}'";
            }

            return null;
        }

        private async Task UpdateClusterManagerAssignmentAsync(Guid clusterId, Guid? oldManagerId, Guid? newManagerId)
        {
            if (oldManagerId == newManagerId)
            {
                return;
            }

            if (oldManagerId.HasValue)
            {
                var oldManager = await _unitOfWork.ClusterManagerRepository.GetEntityByIdAsync(c => c.Id == oldManagerId.Value);
                if (oldManager != null)
                {
                    oldManager.ClusterId = null;
                    oldManager.AssignedDate = null;
                    _unitOfWork.ClusterManagerRepository.Update(oldManager);
                }
            }

            if (!newManagerId.HasValue)
            {
                return;
            }

            var newManager = await _unitOfWork.ClusterManagerRepository.GetEntityByIdAsync(c => c.Id == newManagerId.Value);
            if (newManager == null)
            {
                _logger.LogWarning("Cannot find cluster manager with id: {ClusterManagerId}", newManagerId);
                return;
            }

            newManager.ClusterId = clusterId;
            newManager.AssignedDate = DateTime.UtcNow;
            _unitOfWork.ClusterManagerRepository.Update(newManager);
        }

        private async Task UpdateAgronomyExpertAssignmentAsync(
            Guid clusterId,
            Guid? oldExpertId,
            Guid? newExpertId,
            CancellationToken cancellationToken)
        {
            if (oldExpertId == newExpertId)
            {
                return;
            }

            if (oldExpertId.HasValue)
            {
                var oldExpert = await _unitOfWork.AgronomyExpertRepository.GetAgronomyExpertByIdAsync(oldExpertId.Value, cancellationToken);
                if (oldExpert != null)
                {
                    oldExpert.ClusterId = null;
                    oldExpert.AssignedDate = null;
                    _unitOfWork.AgronomyExpertRepository.Update(oldExpert);
                }
            }

            if (!newExpertId.HasValue)
            {
                return;
            }

            var newExpert = await _unitOfWork.AgronomyExpertRepository.GetAgronomyExpertByIdAsync(newExpertId.Value, cancellationToken);
            if (newExpert == null)
            {
                _logger.LogWarning("Cannot find agronomy expert with id: {AgronomyExpertId}", newExpertId);
                return;
            }

            newExpert.ClusterId = clusterId;
            newExpert.AssignedDate = DateTime.UtcNow;
            _unitOfWork.AgronomyExpertRepository.Update(newExpert);
        }

        private async Task UpdateSupervisorAssignmentsAsync(
            Cluster cluster,
            List<Guid>? requestedSupervisorIds,
            CancellationToken cancellationToken)
        {
            // Both null and empty list mean: remove all supervisors
            var currentSupervisors = cluster.SupervisorsInCluster?.ToList() ?? new List<Supervisor>();
            var currentSupervisorIds = currentSupervisors.Select(s => s.Id).ToHashSet();
            var newSupervisorIds = requestedSupervisorIds?.ToHashSet() ?? new HashSet<Guid>();

            foreach (var supervisor in currentSupervisors.Where(s => !newSupervisorIds.Contains(s.Id)))
            {
                supervisor.ClusterId = null;
                supervisor.AssignedDate = null;
                _unitOfWork.SupervisorRepository.Update(supervisor);
                _logger.LogInformation("Removed supervisor {SupervisorId} from cluster {ClusterId}",
                    supervisor.Id, cluster.Id);
            }

            if (requestedSupervisorIds == null || requestedSupervisorIds.Count == 0)
            {
                return;
            }

            foreach (var supervisorId in newSupervisorIds.Where(id => !currentSupervisorIds.Contains(id)))
            {
                var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(supervisorId, cancellationToken);
                if (supervisor == null)
                {
                    _logger.LogWarning("Cannot find supervisor with id: {SupervisorId}", supervisorId);
                    continue;
                }

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
        }
    }
}
