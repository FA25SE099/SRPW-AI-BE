using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.ClusterFeature.Commands.CreateCluster;
using RiceProduction.Application.ClusterManagerFeature.Commands.CreateClusterManager;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.UavVendorFeature.Commands.CreateUavVendor
{
    public class CreateUavVendorCommandHandler : IRequestHandler<CreateUavVendorCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CreateUavVendorCommandHandler> _logger;
        private readonly IPublisher _publisher;
        private readonly IUnitOfWork _unitOfWork;

        public CreateUavVendorCommandHandler(UserManager<ApplicationUser> userManager, ILogger<CreateUavVendorCommandHandler> logger, IPublisher publisher, IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _logger = logger;
            _publisher = publisher;
            _unitOfWork = unitOfWork;
        }
        public async Task<Result<Guid>> Handle(CreateUavVendorCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Check if user with email already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return Result<Guid>.Failure($"User with email '{request.Email}' already exists");
                }
                // Check if phone number already exists
                var existingPhone = _userManager.Users.Any(u => u.PhoneNumber == request.PhoneNumber);
                if (existingPhone)
                {
                    return Result<Guid>.Failure($"User with phone number '{request.PhoneNumber}' already exists");
                }
                // Create UAV Vendor user
                var uavVendor = new UavVendor
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    EmailConfirmed = true,
                    IsActive = true,
                    VendorName = request.VendorName,
                    BusinessRegistrationNumber = request.BusinessRegistrationNumber,
                    ServiceRatePerHa = request.ServiceRatePerHa,
                    FleetSize = request.FleetSize,
                    ServiceRadius = request.ServiceRadius
                };

                var psw = "Uav123!";
                var result = await _userManager.CreateAsync(uavVendor, psw);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Result<Guid>.Failure($"Failed to create UAV Vendor user: {errors}");
                }
                await _userManager.AddToRoleAsync(uavVendor, "UavVendor");
                _logger.LogInformation("Created UAV Vendor with ID: {UavVendorId}", uavVendor.Id);
                return Result<Guid>.Success(uavVendor.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating UAV Vendor");
                return Result<Guid>.Failure("An error occurred while creating the UAV Vendor");
            }
        }
    }
}
