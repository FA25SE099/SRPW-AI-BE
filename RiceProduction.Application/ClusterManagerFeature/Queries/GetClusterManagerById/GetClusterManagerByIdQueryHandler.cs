using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerById
{
    public class GetClusterManagerByIdQueryHandler : IRequestHandler<GetClusterManagerByIdQuery, Result<ClusterManagerResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetClusterManagerByIdQueryHandler> _logger;

        public GetClusterManagerByIdQueryHandler(IApplicationDbContext context, ILogger<GetClusterManagerByIdQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<ClusterManagerResponse>> Handle(GetClusterManagerByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var clusterManagerResponse = await _context.ClusterManagers
                    .AsNoTracking()
                    .Where(m => m.Id == request.ClusterManagerId)
                    .Select(m => new ClusterManagerResponse
                    {
                        ClusterManagerId = m.Id,
                        ClusterManagerName = m.FullName,
                        ClusterManagerPhoneNumber = m.PhoneNumber,
                        Email = m.Email,
                        ClusterId = m.ClusterId,
                        ClusterName = m.ManagedCluster != null ? m.ManagedCluster.ClusterName : null,
                        AssignedDate = m.AssignedDate
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (clusterManagerResponse == null)
                {
                    return Result<ClusterManagerResponse>.Failure("Cluster manager not found");
                }

                return Result<ClusterManagerResponse>.Success(
                    clusterManagerResponse,
                    "Cluster manager retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting cluster manager");
                return Result<ClusterManagerResponse>.Failure("An error occurred while processing your request");
            }
        }
    }
}