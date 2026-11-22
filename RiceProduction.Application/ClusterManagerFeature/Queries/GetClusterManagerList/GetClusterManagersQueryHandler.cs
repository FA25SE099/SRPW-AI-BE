using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList
{
    public class GetClusterManagersQueryHandler : IRequestHandler<GetClusterManagersQuery, PagedResult<List<ClusterManagerResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetClusterManagersQueryHandler> _logger;

        public GetClusterManagersQueryHandler(IApplicationDbContext context, ILogger<GetClusterManagersQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResult<List<ClusterManagerResponse>>> Handle(GetClusterManagersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.ClusterManagers.AsNoTracking();

                if (request.FreeOrAssigned.HasValue)
                {
                    query = request.FreeOrAssigned.Value
                        ? query.Where(m => !m.ClusterId.HasValue)
                        : query.Where(m => m.ClusterId.HasValue);
                }

                var search = request.Search?.ToLower();
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(m => (m.FullName != null && m.FullName.ToLower().Contains(search)) ||
                                             (m.Email != null && m.Email.ToLower().Contains(search)));
                }

                if (!string.IsNullOrEmpty(request.PhoneNumber))
                {
                    var phoneNumber = request.PhoneNumber;
                    query = query.Where(m => m.PhoneNumber != null && m.PhoneNumber.Contains(phoneNumber));
                }

                var totalCount = await query.CountAsync(cancellationToken);

                var orderedQuery = query.OrderBy(m => m.FullName);
                var pagedQuery = request.PageSize == 0
                    ? orderedQuery
                    : orderedQuery.Skip((request.CurrentPage - 1) * request.PageSize).Take(request.PageSize);

                var clusterManagerResponse = await pagedQuery
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
                    .ToListAsync(cancellationToken);

                return PagedResult<List<ClusterManagerResponse>>.Success(
                    clusterManagerResponse,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Cluster managers retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting cluster managers");
                return PagedResult<List<ClusterManagerResponse>>.Failure("An error occurred while processing your request");
            }
        }
    }
}