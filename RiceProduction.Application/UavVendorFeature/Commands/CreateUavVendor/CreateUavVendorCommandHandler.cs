using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.ClusterFeature.Commands.CreateCluster;
using RiceProduction.Application.ClusterManagerFeature.Commands.CreateClusterManager;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.UavVendorFeature.Events;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RiceProduction.Application.Common.Constants.ApplicationMessages;

namespace RiceProduction.Application.UavVendorFeature.Commands.CreateUavVendor
{
    public class CreateUavVendorCommandHandler : IRequestHandler<CreateUavVendorCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CreateUavVendorCommandHandler> _logger;
        private readonly IPublisher _publisher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        public CreateUavVendorCommandHandler(UserManager<ApplicationUser> userManager, ILogger<CreateUavVendorCommandHandler> logger, IPublisher publisher, IUnitOfWork unitOfWork, IMediator mediator)
        {
            _userManager = userManager;
            _logger = logger;
            _publisher = publisher;
            _unitOfWork = unitOfWork;
            _mediator = mediator;
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
                var uavVendor = new Domain.Entities.UavVendor
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

                var psw = GenerateRandomPassword();
                var result = await _userManager.CreateAsync(uavVendor, psw);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Result<Guid>.Failure($"Failed to create UAV Vendor user: {errors}");
                }
                await _userManager.AddToRoleAsync(uavVendor, "UavVendor");
                _logger.LogInformation("Created UAV Vendor with ID: {UavVendorId}", uavVendor.Id);

                await _mediator.Publish(new UavVendorCreatedEvent
                {
                    UavVendorId = uavVendor.Id,
                    FullName = uavVendor.FullName,
                    PhoneNumber = uavVendor.PhoneNumber,
                    Email = uavVendor.Email,
                    Password = psw,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken
                );
                _logger.LogInformation("Published UavVendorCreatedEvent for UavVendor {UavVendorId} to send credentials email", uavVendor.Id);
                return Result<Guid>.Success(uavVendor.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating UAV Vendor");
                return Result<Guid>.Failure("An error occurred while creating the UAV Vendor");
            }
        }
        private string GenerateRandomPassword()
        {
            const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
            const string digitChars = "0123456789";
            const string specialChars = "!@#$%";

            var random = new Random();
            var password = new char[12];
            password[0] = upperChars[random.Next(upperChars.Length)];
            password[1] = lowerChars[random.Next(lowerChars.Length)];
            password[2] = digitChars[random.Next(digitChars.Length)];
            password[3] = specialChars[random.Next(specialChars.Length)];
            const string allChars = upperChars + lowerChars + digitChars + specialChars;
            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }
            return new string(password.OrderBy(x => random.Next()).ToArray());
        }
    }
}
