using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Application.Common.Models.Response.ClusterResponses;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ClusterFeature.Queries.GetAllClustersPaging
{
    public class GetAllClustersQueryHandler : IRequestHandler<GetAllClustersQuery, PagedResult<List<ClusterResponse>>>
    {
        private readonly ILogger<GetAllClustersQueryHandler> _logger;
        private readonly IApplicationDbContext _context;

        public GetAllClustersQueryHandler(ILogger<GetAllClustersQueryHandler> logger, IApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<PagedResult<List<ClusterResponse>>> Handle(GetAllClustersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.Clusters.AsNoTracking();

                if (!string.IsNullOrEmpty(request.ClusterNameSearch))
                {
                    var clusterNameSearch = request.ClusterNameSearch.ToLower();
                    query = query.Where(c => c.ClusterName.ToLower().Contains(clusterNameSearch));
                }

                if (!string.IsNullOrEmpty(request.ManagerExpertNameSearch))
                {
                    var managerExpertSearch = request.ManagerExpertNameSearch.ToLower();
                    query = query.Where(c =>
                        (c.ClusterManager != null && c.ClusterManager.FullName != null && c.ClusterManager.FullName.ToLower().Contains(managerExpertSearch)) ||
                        (c.AgronomyExpert != null && c.AgronomyExpert.FullName != null && c.AgronomyExpert.FullName.ToLower().Contains(managerExpertSearch)) ||
                        (c.ClusterManager != null && c.ClusterManager.Email != null && c.ClusterManager.Email.ToLower().Contains(managerExpertSearch)) ||
                        (c.AgronomyExpert != null && c.AgronomyExpert.Email != null && c.AgronomyExpert.Email.ToLower().Contains(managerExpertSearch)));
                }

                if (!string.IsNullOrEmpty(request.PhoneNumber))
                {
                    var phoneNumber = request.PhoneNumber;
                    query = query.Where(c =>
                        (c.ClusterManager != null && c.ClusterManager.PhoneNumber != null && c.ClusterManager.PhoneNumber.Contains(phoneNumber)) ||
                        (c.AgronomyExpert != null && c.AgronomyExpert.PhoneNumber != null && c.AgronomyExpert.PhoneNumber.Contains(phoneNumber)));
                }

                var totalCount = await query.CountAsync(cancellationToken);

                query = request.SortBy switch
                {
                    SortBy.NameAscending => query.OrderBy(c => c.ClusterName),
                    SortBy.NameDescending => query.OrderByDescending(c => c.ClusterName),
                    SortBy.DateCreatedAscending => query.OrderBy(c => c.CreatedAt),
                    _ => query.OrderByDescending(c => c.CreatedAt)
                };

                var clusterResponses = await query
                    .Skip((request.CurrentPage - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(c => new ClusterResponse
                    {
                        ClusterId = c.Id,
                        ClusterName = c.ClusterName,
                        ClusterManagerId = c.ClusterManagerId,
                        AgronomyExpertId = c.AgronomyExpertId,
                        ClusterManagerName = c.ClusterManager != null ? c.ClusterManager.FullName : null,
                        ClusterManagerPhoneNumber = c.ClusterManager != null ? c.ClusterManager.PhoneNumber : null,
                        ClusterManagerEmail = c.ClusterManager != null ? c.ClusterManager.Email : null,
                        AgronomyExpertName = c.AgronomyExpert != null ? c.AgronomyExpert.FullName : null,
                        AgronomyExpertPhoneNumber = c.AgronomyExpert != null ? c.AgronomyExpert.PhoneNumber : null,
                        AgronomyExpertEmail = c.AgronomyExpert != null ? c.AgronomyExpert.Email : null,
                        Area = c.Area,
                        Supervisors = c.SupervisorsInCluster.Select(s => new SupervisorSummary
                        {
                            SupervisorId = s.Id,
                            FullName = s.FullName,
                            PhoneNumber = s.PhoneNumber,
                            Email = s.Email,
                            CurrentFarmerCount = s.CurrentFarmerCount,
                            MaxFarmerCapacity = s.MaxFarmerCapacity,
                            AssignedDate = s.AssignedDate
                        }).ToList()
                    })
                    .ToListAsync(cancellationToken);

                return PagedResult<List<ClusterResponse>>.Success(
                    clusterResponses,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Clusters retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting clusters");
                return PagedResult<List<ClusterResponse>>.Failure("An error occurred while processing your request");
            }
        }
    }
}