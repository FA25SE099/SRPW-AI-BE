using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;
using RiceProduction.Application.UavVendorFeature.Commands.UpdateUavVendor;
using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorById
{
    public class GetUavVendorByIdQueryHandler : IRequestHandler<GetUavVendorByIdQuery, Result<UavVendorDetailResponse>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<GetUavVendorByIdQueryHandler> _logger;
        private readonly IPublisher _publisher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUavVendorRepository _uavVendorRepo;

        public GetUavVendorByIdQueryHandler(UserManager<ApplicationUser> userManager, ILogger<GetUavVendorByIdQueryHandler> logger, IPublisher publisher, IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _logger = logger;
            _publisher = publisher;
            _unitOfWork = unitOfWork;
            _uavVendorRepo = _unitOfWork.UavVendorRepository;
        }

        public async Task<Result<UavVendorDetailResponse>> Handle(GetUavVendorByIdQuery request, CancellationToken cancellationToken)
        {
            var uavVendor = await _uavVendorRepo.GetUavVendorByIdAsync(request.UavVendorId, cancellationToken);
            if (uavVendor == null)
            {
                return Result<UavVendorDetailResponse>.Failure("UAV Vendor not found");
            }
            var response = new UavVendorDetailResponse
            {
                FullName = uavVendor.FullName,
                Email = uavVendor.Email,
                PhoneNumber = uavVendor.PhoneNumber,
                VendorName = uavVendor.VendorName,
                BusinessRegistrationNumber = uavVendor.BusinessRegistrationNumber,
                ServiceRatePerHa = uavVendor.ServiceRatePerHa,
                FleetSize = uavVendor.FleetSize,
                ServiceRadius = uavVendor.ServiceRadius,
                EquipmentSpecs = uavVendor.EquipmentSpecs,
                OperatingSchedule = uavVendor.OperatingSchedule
            };
            return Result<UavVendorDetailResponse>.Success(response);
        }
    }
}
