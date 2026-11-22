using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterIdByManagerId
{
    public class GetClusterIdByManagerIdQueryHandler : IRequestHandler<GetClusterIdByManagerIdQuery, Result<Guid?>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetClusterIdByManagerIdQueryHandler> _logger;

        public GetClusterIdByManagerIdQueryHandler(
            IApplicationDbContext context,
            ILogger<GetClusterIdByManagerIdQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<Guid?>> Handle(GetClusterIdByManagerIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var clusterManager = await _context.ClusterManagers
                    .AsNoTracking()
                    .Where(m => m.Id == request.ClusterManagerId)
                    .Select(m => new { m.ClusterId })
                    .FirstOrDefaultAsync(cancellationToken);

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