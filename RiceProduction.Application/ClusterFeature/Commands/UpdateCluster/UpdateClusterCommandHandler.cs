using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ClusterFeature.Commands.UpdateCluster
{
    public class UpdateClusterCommandHandler : IRequestHandler<UpdateClusterCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<UpdateClusterCommandHandler> _logger;

        public UpdateClusterCommandHandler(IApplicationDbContext context, ILogger<UpdateClusterCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateClusterCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var cluster = await _context.Clusters
                    .Include(c => c.SupervisorsInCluster)
                    .FirstOrDefaultAsync(c => c.Id == request.ClusterId, cancellationToken);
                if (cluster == null)
                {
                    return Result<Guid>.Failure("Cluster not found");
                }

                var duplicateError = await ValidateUniquenessAsync(request, cluster.Id, cancellationToken);
                if (duplicateError != null)
                {
                    return Result<Guid>.Failure(duplicateError);
                }

                var oldClusterManagerId = cluster.ClusterManagerId;
                var oldAgronomyExpertId = cluster.AgronomyExpertId;

                cluster.ClusterName = request.ClusterName;
                cluster.ClusterManagerId = request.ClusterManagerId;
                cluster.AgronomyExpertId = request.AgronomyExpertId;

                await UpdateClusterManagerAssignmentAsync(cluster.Id, oldClusterManagerId, request.ClusterManagerId, cancellationToken);
                await UpdateAgronomyExpertAssignmentAsync(cluster.Id, oldAgronomyExpertId, request.AgronomyExpertId, cancellationToken);
                await UpdateSupervisorAssignmentsAsync(cluster, request.SupervisorIds, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                return Result<Guid>.Success(cluster.Id, "Cluster updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating cluster");
                return Result<Guid>.Failure("An error occurred while processing your request");
            }
        }

        private async Task<string?> ValidateUniquenessAsync(
            UpdateClusterCommand request,
            Guid clusterId,
            CancellationToken cancellationToken)
        {
            var duplicateName = await _context.Clusters
                .AsNoTracking()
                .AnyAsync(m => m.ClusterName == request.ClusterName && m.Id != clusterId, cancellationToken);
            if (duplicateName)
            {
                return $"Cluster with name '{request.ClusterName}' already exists";
            }

            if (request.ClusterManagerId.HasValue)
            {
                var duplicateManagerClusterId = await _context.Clusters
                    .AsNoTracking()
                    .Where(m => m.ClusterManagerId == request.ClusterManagerId && m.Id != clusterId)
                    .Select(m => (Guid?)m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (duplicateManagerClusterId.HasValue)
                {
                    return $"Cluster manager ID = '{request.ClusterManagerId}' already managing another cluster with ID = '{duplicateManagerClusterId.Value}'";
                }
            }

            if (request.AgronomyExpertId.HasValue)
            {
                var duplicateExpertClusterId = await _context.Clusters
                    .AsNoTracking()
                    .Where(m => m.AgronomyExpertId == request.AgronomyExpertId && m.Id != clusterId)
                    .Select(m => (Guid?)m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (duplicateExpertClusterId.HasValue)
                {
                    return $"Agronomy Expert ID = '{request.AgronomyExpertId}' already managing another cluster with ID = '{duplicateExpertClusterId.Value}'";
                }
            }

            return null;
        }

        private async Task UpdateClusterManagerAssignmentAsync(
            Guid clusterId,
            Guid? oldManagerId,
            Guid? newManagerId,
            CancellationToken cancellationToken)
        {
            if (oldManagerId == newManagerId)
            {
                return;
            }

            if (oldManagerId.HasValue)
            {
                var oldManager = await _context.ClusterManagers
                    .FirstOrDefaultAsync(c => c.Id == oldManagerId.Value, cancellationToken);
                if (oldManager != null)
                {
                    oldManager.ClusterId = null;
                    oldManager.AssignedDate = null;
                }
            }

            if (!newManagerId.HasValue)
            {
                return;
            }

            var newManager = await _context.ClusterManagers
                .FirstOrDefaultAsync(c => c.Id == newManagerId.Value, cancellationToken);
            if (newManager == null)
            {
                _logger.LogWarning("Cannot find cluster manager with id: {ClusterManagerId}", newManagerId);
                return;
            }

            newManager.ClusterId = clusterId;
            newManager.AssignedDate = DateTime.UtcNow;
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
                var oldExpert = await _context.AgronomyExperts
                    .FirstOrDefaultAsync(e => e.Id == oldExpertId.Value, cancellationToken);
                if (oldExpert != null)
                {
                    oldExpert.ClusterId = null;
                    oldExpert.AssignedDate = null;
                }
            }

            if (!newExpertId.HasValue)
            {
                return;
            }

            var newExpert = await _context.AgronomyExperts
                .FirstOrDefaultAsync(e => e.Id == newExpertId.Value, cancellationToken);
            if (newExpert == null)
            {
                _logger.LogWarning("Cannot find agronomy expert with id: {AgronomyExpertId}", newExpertId);
                return;
            }

            newExpert.ClusterId = clusterId;
            newExpert.AssignedDate = DateTime.UtcNow;
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
                _logger.LogInformation("Removed supervisor {SupervisorId} from cluster {ClusterId}",
                    supervisor.Id, cluster.Id);
            }

            if (requestedSupervisorIds == null || requestedSupervisorIds.Count == 0)
            {
                return;
            }

            foreach (var supervisorId in newSupervisorIds.Where(id => !currentSupervisorIds.Contains(id)))
            {
                var supervisor = await _context.Supervisors
                    .FirstOrDefaultAsync(s => s.Id == supervisorId, cancellationToken);
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
                _logger.LogInformation("Assigned supervisor {SupervisorId} to cluster {ClusterId}",
                    supervisorId, cluster.Id);
            }
        }
    }
}
