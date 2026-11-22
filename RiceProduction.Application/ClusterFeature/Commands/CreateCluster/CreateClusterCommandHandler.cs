using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ClusterFeature.Commands.CreateCluster
{
    public class CreateClusterCommandHandler : IRequestHandler<CreateClusterCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<CreateClusterCommandHandler> _logger;

        public CreateClusterCommandHandler(IApplicationDbContext context, ILogger<CreateClusterCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateClusterCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var duplicateName = await _context.Clusters
                    .AsNoTracking()
                    .AnyAsync(m => m.ClusterName == request.ClusterName, cancellationToken);
                if (duplicateName)
                {
                    return Result<Guid>.Failure($"Cluster with name '{request.ClusterName}' already exists");
                }

                if (request.ClusterManagerId.HasValue)
                {
                    var duplicateManagerClusterId = await _context.Clusters
                        .AsNoTracking()
                        .Where(m => m.ClusterManagerId == request.ClusterManagerId)
                        .Select(m => (Guid?)m.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (duplicateManagerClusterId.HasValue)
                    {
                        return Result<Guid>.Failure($"Cluster manager ID = '{request.ClusterManagerId}' already managing another cluster with ID = '{duplicateManagerClusterId.Value}'");
                    }
                }

                if (request.AgronomyExpertId.HasValue)
                {
                    var duplicateExpertClusterId = await _context.Clusters
                        .AsNoTracking()
                        .Where(m => m.AgronomyExpertId == request.AgronomyExpertId)
                        .Select(m => (Guid?)m.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (duplicateExpertClusterId.HasValue)
                    {
                        return Result<Guid>.Failure($"Agronomy Expert ID = '{request.AgronomyExpertId}' already managing another cluster with ID = '{duplicateExpertClusterId.Value}'");
                    }
                }

                var id = Guid.NewGuid();
                var newCluster = new Cluster
                {
                    Id = id,
                    ClusterName = request.ClusterName,
                    ClusterManagerId = request.ClusterManagerId,
                    AgronomyExpertId = request.AgronomyExpertId
                };

                _context.Clusters.Add(newCluster);

                // Update ClusterManager
                if (request.ClusterManagerId.HasValue)
                {
                    var clusterManager = await _context.ClusterManagers
                        .FirstOrDefaultAsync(c => c.Id == request.ClusterManagerId.Value, cancellationToken);
                    if (clusterManager != null)
                    {
                        clusterManager.ClusterId = newCluster.Id;
                        clusterManager.AssignedDate = DateTime.UtcNow;
                    }
                    else
                    {
                        _logger.LogWarning("Cannot find cluster manager with id: {ClusterManagerId}", request.ClusterManagerId);
                    }
                }

                // Update AgronomyExpert
                if (request.AgronomyExpertId.HasValue)
                {
                    var agronomyExpert = await _context.AgronomyExperts
                        .FirstOrDefaultAsync(e => e.Id == request.AgronomyExpertId.Value, cancellationToken);
                    if (agronomyExpert != null)
                    {
                        agronomyExpert.ClusterId = newCluster.Id;
                        agronomyExpert.AssignedDate = DateTime.UtcNow;
                    }
                    else
                    {
                        _logger.LogWarning("Cannot find agronomy expert with id: {AgronomyExpertId}", request.AgronomyExpertId);
                    }
                }

                // Assign Supervisors to Cluster
                if (request.SupervisorIds != null && request.SupervisorIds.Any())
                {
                    foreach (var supervisorId in request.SupervisorIds)
                    {
                        var supervisor = await _context.Supervisors
                            .FirstOrDefaultAsync(s => s.Id == supervisorId, cancellationToken);
                        if (supervisor != null)
                        {
                            // Check if supervisor is already assigned to another cluster
                            if (supervisor.ClusterId.HasValue)
                            {
                                _logger.LogWarning("Supervisor {SupervisorId} is already assigned to cluster {ClusterId}",
                                    supervisorId, supervisor.ClusterId);
                                continue;
                            }

                            supervisor.ClusterId = newCluster.Id;
                            supervisor.AssignedDate = DateTime.UtcNow;
                            _logger.LogInformation("Assigned supervisor {SupervisorId} to cluster {ClusterId}",
                                supervisorId, newCluster.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Cannot find supervisor with id: {SupervisorId}", supervisorId);
                        }
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Created Cluster with ID: {ClusterId}", newCluster.Id);
                return Result<Guid>.Success(newCluster.Id, "Cluster created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Cluster");
                return Result<Guid>.Failure("Failed to create Cluster");
            }
        }
    }
}
