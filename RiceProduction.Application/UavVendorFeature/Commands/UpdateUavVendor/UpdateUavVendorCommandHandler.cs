using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.ClusterFeature.Commands.UpdateCluster;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.UavVendorFeature.Commands.CreateUavVendor;
using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.UavVendorFeature.Commands.UpdateUavVendor
{
    public class UpdateUavVendorCommandHandler : IRequestHandler<UpdateUavVendorCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUavVendorRepository _uavVendorRepo;
        private readonly ILogger<UpdateUavVendorCommandHandler> _logger;
        private readonly IPublisher _publisher;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateUavVendorCommandHandler(UserManager<ApplicationUser> userManager, ILogger<UpdateUavVendorCommandHandler> logger, IPublisher publisher, IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _logger = logger;
            _publisher = publisher;
            _unitOfWork = unitOfWork;
            _uavVendorRepo = _unitOfWork.UavVendorRepository;
        }

        public async Task<Result<Guid>> Handle(UpdateUavVendorCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var uavVendor = await _uavVendorRepo.GetUavVendorByIdAsync(request.UavVendorId, cancellationToken);
                if (uavVendor == null)
                {
                    return Result<Guid>.Failure("UAV Vendor not found");
                }
                uavVendor.FullName = request.FullName;
                uavVendor.PhoneNumber = request.PhoneNumber;
                uavVendor.Email = request.Email;
                uavVendor.VendorName = request.VendorName;
                uavVendor.BusinessRegistrationNumber = request.BusinessRegistrationNumber;
                uavVendor.ServiceRatePerHa = request.ServiceRatePerHa;
                uavVendor.FleetSize = request.FleetSize;
                uavVendor.ServiceRadius = request.ServiceRadius;
                uavVendor.EquipmentSpecs = request.EquipmentSpecs;
                uavVendor.OperatingSchedule = request.OperatingSchedule;

                await _userManager.UpdateAsync(uavVendor);
                var uavVendorNew = await _uavVendorRepo.GetUavVendorByIdAsync(uavVendor.Id);
                if (uavVendorNew != null && (uavVendorNew.FullName != request.FullName ||
                    uavVendorNew.PhoneNumber != request.PhoneNumber ||
                    uavVendorNew.VendorName != request.VendorName ||
                    uavVendorNew.BusinessRegistrationNumber != request.BusinessRegistrationNumber ||
                    uavVendorNew.ServiceRatePerHa != request.ServiceRatePerHa ||
                    uavVendorNew.FleetSize != request.FleetSize ||
                    uavVendorNew.ServiceRadius != request.ServiceRadius ||
                    uavVendorNew.EquipmentSpecs != request.EquipmentSpecs ||
                    uavVendorNew.OperatingSchedule != request.OperatingSchedule))
                {
                    return Result<Guid>.Failure("Failed to update UAV Vendor");
                }
                return Result<Guid>.Success(uavVendor.Id, "UAV Vendor updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating UAV Vendor");
                return Result<Guid>.Failure("Error updating UAV Vendor");
            }
        }
    }
}
