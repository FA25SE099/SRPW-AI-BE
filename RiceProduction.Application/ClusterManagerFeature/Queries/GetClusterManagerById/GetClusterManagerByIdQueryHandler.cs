using Microsoft.Extensions.Logging;
using RiceProduction.Application.AgronomyExpertFeature.Queries.GetAgronomyExpertById;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerById
{
    public class GetClusterManagerByIdQueryHandler : IRequestHandler<GetClusterManagerByIdQuery, Result<ClusterManagerResponse>>
    {
        private readonly IClusterManagerRepository _clusterRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetClusterManagerByIdQueryHandler> _logger;

        public GetClusterManagerByIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetClusterManagerByIdQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _clusterRepo = _unitOfWork.ClusterManagerRepository;
            _logger = logger;
        }

        public async Task<Result<ClusterManagerResponse>> Handle(GetClusterManagerByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var cluster = await _clusterRepo.GetClusterManagerByIdAsync(
                    request.ClusterManagerId,
                    cancellationToken);

                var clusterManagerResponse = new ClusterManagerResponse
                {
                    ClusterManagerId = cluster.Id,
                    ClusterManagerName = cluster.FullName,
                    ClusterManagerPhoneNumber = cluster.PhoneNumber,
                    Email = cluster.Email,
                    ClusterId = cluster.ClusterId,
                    ClusterName = cluster.ManagedCluster?.ClusterName,
                    AssignedDate = cluster.AssignedDate
                };

                return Result<ClusterManagerResponse>.Success(
                    clusterManagerResponse,
                    "Agronomy experts retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting agronomy experts");
                return Result<ClusterManagerResponse>.Failure("An error occurred while processing your request");
            }
        }
    }
}
