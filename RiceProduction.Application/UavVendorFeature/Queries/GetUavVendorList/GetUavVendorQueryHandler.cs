using Microsoft.Extensions.Logging;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorList
{
    public class GetUavVendorQueryHandler : IRequestHandler<GetUavVendorQuery, PagedResult<List<UavVendorResponse>>>
    {
        private readonly IUavVendorRepository _uavVendorRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetClusterManagersQueryHandler> _logger;

        public GetUavVendorQueryHandler(IUnitOfWork unitOfWork, ILogger<GetClusterManagersQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _uavVendorRepo = _unitOfWork.UavVendorRepository;
            _logger = logger;
        }
        public async Task<PagedResult<List<UavVendorResponse>>> Handle(GetUavVendorQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var (pagedUavVendors, totalCount) = await _uavVendorRepo.GetAllUavVendorByNameOrEmailAndPhoneNumberAndByGroupIdOrClusterIdOrNamePagingAsync(
                    request.CurrentPage,
                    request.PageSize,
                    request.NameEmailSearch,
                    request.GroupClusterSearch,
                    request.PhoneNumber,
                    cancellationToken);
                var uavVendorResponse = pagedUavVendors.Select(uavVendor => new UavVendorResponse
                {
                    UavVendorId = uavVendor.Id,
                    UavVendorFullName = uavVendor.FullName,
                    VendorName = uavVendor.VendorName,
                    UavVendorPhoneNumber = uavVendor.PhoneNumber,
                    Email = uavVendor.Email
                }).ToList();
                return PagedResult<List<UavVendorResponse>>.Success(
                    uavVendorResponse,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Uav Vendors retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving UAV Vendor list");
                throw;
            }
        }
    }
}
