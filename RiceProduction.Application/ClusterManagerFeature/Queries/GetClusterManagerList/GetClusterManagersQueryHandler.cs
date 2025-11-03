using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList
{
    public class GetClusterManagersQueryHandler : IRequestHandler<GetClusterManagersQuery, PagedResult<List<ClusterManagerResponse>>>
    {
        private readonly IClusterManagerRepository _clusterRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetClusterManagersQueryHandler> _logger;

        public GetClusterManagersQueryHandler(IClusterManagerRepository clusterRepo, IUnitOfWork unitOfWork, ILogger<GetClusterManagersQueryHandler> logger)
        {
            _clusterRepo = clusterRepo;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        public async Task<PagedResult<List<ClusterManagerResponse>>> Handle(GetClusterManagersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _clusterRepo.GetAllClusterManagerAssignedOrNotByNameOrEmailAndPhoneNumberPagingAsync(
                    request.CurrentPage,
                    request.PageSize,
                    request.Search,
                    request.PhoneNumber,
                    request.FreeOrAssigned,
                    cancellationToken);
                var clusterManagerResponse = new List<ClusterManagerResponse>();
                foreach (var clusterManager in result)
                {
                    clusterManagerResponse.Add(new ClusterManagerResponse
                    {
                        ClusterManagerName = clusterManager.FullName,
                        ClusterManagerPhoneNumber = clusterManager.PhoneNumber,
                        ClusterId = clusterManager.ClusterId,
                        AssignedDate = clusterManager.AssignedDate,
                    });
                }
                return PagedResult<List<ClusterManagerResponse>>.Success(
                    clusterManagerResponse,
                    request.CurrentPage,
                    request.PageSize,
                    result.Count(),
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
