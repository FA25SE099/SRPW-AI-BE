using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialFeature.Commands.CreateMaterial;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterFeature.Commands.CreateCluster
{
    public class CreateClusterCommandHandler : IRequestHandler<CreateClusterCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateClusterCommandHandler> _logger;
        public CreateClusterCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateClusterCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateClusterCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var clusterRepo = _unitOfWork.Repository<Cluster>();

                var duplicate = await clusterRepo.FindAsync(m => m.ClusterName == request.ClusterName);
                if (duplicate != null)
                {
                    return Result<Guid>.Failure($"Cluster with name '{request.ClusterName}' already exists");
                }

                duplicate = await clusterRepo.FindAsync(m => m.ClusterManagerId == request.ClusterManagerId);
                if (duplicate != null)
                {
                    return Result<Guid>.Failure($"Cluster manager ID = '{request.ClusterManagerId}' already managing another cluster with ID = '{duplicate.Id}'");
                }

                duplicate = await clusterRepo.FindAsync(m => m.AgronomyExpertId == request.AgronomyExpertId);
                if (duplicate != null)
                {
                    return Result<Guid>.Failure($"Agronomy Expert ID = '{request.AgronomyExpertId}' already managing another cluster with ID = '{duplicate.Id}'");
                }
                var id = await clusterRepo.GenerateNewGuid(Guid.NewGuid());
                var newCluster = new Cluster
                {
                    ClusterName = request.ClusterName,
                    ClusterManagerId = request.ClusterManagerId,
                    AgronomyExpertId = request.AgronomyExpertId
                };
                
                // Add the cluster first
                await clusterRepo.AddAsync(newCluster);

                // Update ClusterManager
                if (request.ClusterManagerId.HasValue)
                {
                    var clusterManager = await _unitOfWork.ClusterManagerRepository.GetEntityByIdAsync(c => c.Id == request.ClusterManagerId);
                    if (clusterManager != null)
                    {
                        clusterManager.ClusterId = newCluster.Id;
                        clusterManager.AssignedDate = DateTime.UtcNow;
                        _unitOfWork.ClusterManagerRepository.Update(clusterManager);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot find cluster manager with id: {ClusterManagerId}", request.ClusterManagerId);
                    }
                }

                // Update AgronomyExpert
                if (request.AgronomyExpertId.HasValue)
                {
                    var agronomyExpert = await _unitOfWork.AgronomyExpertRepository.GetAgronomyExpertByIdAsync(request.AgronomyExpertId.Value, cancellationToken);
                    if (agronomyExpert != null)
                    {
                        agronomyExpert.ClusterId = newCluster.Id;
                        agronomyExpert.AssignedDate = DateTime.UtcNow;
                        _unitOfWork.AgronomyExpertRepository.Update(agronomyExpert);
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
                        var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(supervisorId, cancellationToken);
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
                            _unitOfWork.SupervisorRepository.Update(supervisor);
                            _logger.LogInformation("Assigned supervisor {SupervisorId} to cluster {ClusterId}", 
                                supervisorId, newCluster.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Cannot find supervisor with id: {SupervisorId}", supervisorId);
                        }
                    }
                }

                await _unitOfWork.CompleteAsync();
                
                if (await clusterRepo.ExistsAsync(c => c.Id == id)) { 
                    _logger.LogInformation("Created Cluster with ID: {ClusterId}", id);
                }
                else
                {
                    _logger.LogWarning("Cluster with ID: {ClusterId} was not found after creation", id);
                }
                return Result<Guid>.Success(id, "Cluster created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Cluster");
                return Result<Guid>.Failure("Failed to create Cluster");
            }
        }
    }
}
