using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.UavVendorFeature.Commands.UpdateUavVendor
{
    public class UpdateUavVendorCommandHandler : IRequestHandler<UpdateUavVendorCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IApplicationDbContext _context;
        private readonly ILogger<UpdateUavVendorCommandHandler> _logger;

        public UpdateUavVendorCommandHandler(
            UserManager<ApplicationUser> userManager,
            IApplicationDbContext context,
            ILogger<UpdateUavVendorCommandHandler> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateUavVendorCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var uavVendor = await _context.UavVendors
                    .FirstOrDefaultAsync(v => v.Id == request.UavVendorId, cancellationToken);
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

                var updateResult = await _userManager.UpdateAsync(uavVendor);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                    return Result<Guid>.Failure($"Failed to update UAV Vendor: {errors}");
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
