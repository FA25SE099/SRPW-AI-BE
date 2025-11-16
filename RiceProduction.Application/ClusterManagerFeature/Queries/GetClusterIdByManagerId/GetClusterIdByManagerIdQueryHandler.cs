using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterIdByManagerId
{
    public class GetClusterIdByManagerIdQueryHandler : IRequestHandler<GetClusterIdByManagerIdQuery, Result<Guid?>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClusterManagerRepository _clusterManagerRepository;
        private readonly ILogger<GetClusterIdByManagerIdQueryHandler> _logger;

        public GetClusterIdByManagerIdQueryHandler(
            IUnitOfWork unitOfWork, 
            ILogger<GetClusterIdByManagerIdQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _clusterManagerRepository = _unitOfWork.ClusterManagerRepository;
            _logger = logger;
        }

        public async Task<Result<Guid?>> Handle(GetClusterIdByManagerIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var clusterManager = await _clusterManagerRepository
                    .GetClusterManagerByIdAsync(request.ClusterManagerId, cancellationToken);

                if (clusterManager == null)
                {
                    _logger.LogWarning("Cluster Manager with ID {ClusterManagerId} not found", request.ClusterManagerId);
                    return Result<Guid?>.Failure($"Cluster Manager with ID {request.ClusterManagerId} not found");
                }

                if (clusterManager.ClusterId == null)
                {
                    _logger.LogInformation("Cluster Manager {ClusterManagerId} is not assigned to any cluster", request.ClusterManagerId);
                    return Result<Guid?>.Success(null, "Cluster Manager is not assigned to any cluster");
                }

                return Result<Guid?>.Success(clusterManager.ClusterId, "Cluster ID retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting cluster ID for manager {ClusterManagerId}", request.ClusterManagerId);
                return Result<Guid?>.Failure("An error occurred while processing your request");
            }
        }
    }
}
