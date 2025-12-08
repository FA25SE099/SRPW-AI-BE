using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Application.Common.Models.Response.ClusterResponses;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterFeature.Queries.GetAllClustersPaging
{
    public class GetAllClustersQueryHandler : IRequestHandler<GetAllClustersQuery, PagedResult<List<ClusterResponse>>>
    {
        private readonly ILogger<GetAllClustersQueryHandler> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClusterManagerRepository _clusterManagerRepository;
        private readonly IAgronomyExpertRepository _agronomyExpertRepository;

        public GetAllClustersQueryHandler(ILogger<GetAllClustersQueryHandler> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _clusterManagerRepository = _unitOfWork.ClusterManagerRepository;
            _agronomyExpertRepository = _unitOfWork.AgronomyExpertRepository;
        }
        public async Task<PagedResult<List<ClusterResponse>>> Handle(GetAllClustersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var clusterRepo = _unitOfWork.ClusterRepository;
                var (clusterList, totalCount) = await clusterRepo.GetAllClusterPagedSearchSortAsync(
                    request.CurrentPage,
                    request.PageSize,
                    request.ClusterNameSearch,
                    request.ManagerExpertNameSearch,
                    request.PhoneNumber,
                    request.SortBy,
                    cancellationToken);
                var clusterResponses = clusterList.Select(c => new ClusterResponse
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
                    Supervisors = c.SupervisorsInCluster?.Select(s => new SupervisorSummary
                    {
                        SupervisorId = s.Id,
                        FullName = s.FullName,
                        PhoneNumber = s.PhoneNumber,
                        Email = s.Email,
                        CurrentFarmerCount = s.CurrentFarmerCount,
                        MaxFarmerCapacity = s.MaxFarmerCapacity,
                        AssignedDate = s.AssignedDate
                    }).ToList()
                }).ToList();
                return PagedResult<List<ClusterResponse>>.Success(
                    clusterResponses,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Cluster managers retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting clusters");
                return PagedResult<List<ClusterResponse>>.Failure("An error occurred while processing your request");
            }
        }
    }
}
