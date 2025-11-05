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
                cluster.ClusterName = request.ClusterName;
                cluster.ClusterManagerId = request.ClusterManagerId;
                cluster.AgronomyExpertId = request.AgronomyExpertId;

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
