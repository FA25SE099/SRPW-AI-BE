using Microsoft.Extensions.Logging;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;
using RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForClusterManager;
using RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorList;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForAdmin
{
    public class GetAllSupervisorForAdminQueryHandler : IRequestHandler<GetAllSupervisorForAdminQuery, PagedResult<List<SupervisorResponse>>>
    {
        private readonly ISupervisorRepository _supervisorRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetAllSupervisorForAdminQueryHandler> _logger;

        public GetAllSupervisorForAdminQueryHandler(IUnitOfWork unitOfWork, ILogger<GetAllSupervisorForAdminQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _supervisorRepo = _unitOfWork.SupervisorRepository;
            _logger = logger;
        }

        public async Task<PagedResult<List<SupervisorResponse>>> Handle(GetAllSupervisorForAdminQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var (pagedSupervisors, totalCount) = await _supervisorRepo.GetAllSupervisorByNameOrEmailAndPhoneNumberAndByGroupOrClusterOrFarmerOrPlotOrNamePagingAsync(
                    request.CurrentPage,
                    request.PageSize,
                    request.SearchNameOrEmail,
                    request.AdvancedSearch,
                    request.SearchPhoneNumber,
                    cancellationToken);
                var uavVendorResponse = pagedSupervisors.Select(supervisor => new SupervisorResponse
                {
                    SupervisorId = supervisor.Id,
                    Address = supervisor.Address,
                    CurrentFarmerCount = supervisor.CurrentFarmerCount,
                    Email = supervisor.Email,
                    FullName = supervisor.FullName,
                    LastActivityAt = supervisor.LastActivityAt,
                    PhoneNumber = supervisor.PhoneNumber
                }).ToList();
                return PagedResult<List<SupervisorResponse>>.Success(
                    uavVendorResponse,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Supervisors retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Supervisor list");
                throw;
            }
        }
    }
}
