using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Events;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetById;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.FarmerFeature.Command.CreateFarmer
{
    public class CreateFarmersCommandHandler : IRequestHandler<CreateFarmersCommand, Result<FarmerDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateFarmersCommand> _logger;
        private readonly IFarmerRepository _farmerRepository;
        private readonly IMapper _mapper;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMediator _mediator;
        
        public CreateFarmersCommandHandler(
            IUnitOfWork unitOfWork, 
            ILogger<CreateFarmersCommand> logger, 
            IFarmerRepository farmerRepository, 
            IMapper mapper, 
            UserManager<ApplicationUser> userManager,
            IMediator mediator)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _farmerRepository = farmerRepository;
            _mapper = mapper;
            _userManager = userManager;
            _mediator = mediator;
        }

        public async Task<Result<FarmerDTO>> Handle(CreateFarmersCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Creating farmer: {FullName}", request.FullName);
                
                // Check if phone number already exists
                var phoneNumberExist = await _unitOfWork.FarmerRepository.GetFarmerByPhoneNumber(request.PhoneNumber);
                if (phoneNumberExist != null)
                {
                    _logger.LogError("Phone number {PhoneNumber} already exist", request.PhoneNumber);
                    return Result<FarmerDTO>.Failure($"Số điện thoại {request.PhoneNumber} đã tồn tại.");
                }

                // Check if username is already taken
                var existingUser = await _userManager.FindByNameAsync(request.PhoneNumber);
                if (existingUser != null)
                {
                    _logger.LogError("Username {PhoneNumber} already taken", request.PhoneNumber);
                    return Result<FarmerDTO>.Failure($"Số điện thoại {request.PhoneNumber} đã được sử dụng làm tài khoản.");
                }
                Guid? clusterId = null;
                var clusterManagerId = request.ClusterManagerId;
                if (clusterManagerId.HasValue)
                {
                    var clusterManager = await _unitOfWork.ClusterManagerRepository
                        .GetClusterManagerByIdAsync(clusterManagerId.Value, cancellationToken);
                    clusterId = clusterManager?.ClusterId;
                }

                const string TEMP_PASSWORD = "Farmer@123";

                var newFarmer = new Farmer
                {
                    Id = Guid.NewGuid(),
                    FullName = request.FullName,
                    UserName = request.PhoneNumber,
                    PhoneNumber = request.PhoneNumber,
                    Email = request.Email,
                    Address = request.Address,
                    FarmCode = request.FarmCode,
                    IsActive = true,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true,
                    IsVerified = true,
                    ClusterId = clusterId,
                };

                // Create user with password
                var result = await _userManager.CreateAsync(newFarmer, TEMP_PASSWORD);
                if (!result.Succeeded)
                {
                    var error = result.Errors.Select(e => e.Description).ToList();
                    _logger.LogError("Failed to create farmer: {Errors}", string.Join(", ", error));
                    return Result<FarmerDTO>.Failure(error);
                }

                // Assign Farmer role
                var roleResult = await _userManager.AddToRoleAsync(newFarmer, UserRole.Farmer.ToString());
                if (!roleResult.Succeeded)
                {
                    // Rollback user creation if role assignment fails
                    await _userManager.DeleteAsync(newFarmer);
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to assign role to farmer: {Errors}", errors);
                    return Result<FarmerDTO>.Failure($"Failed to assign role: {errors}");
                }

                _logger.LogInformation("Farmer created successfully: {FarmerId}, {FullName}", newFarmer.Id, newFarmer.FullName);

                // Publish event for email notification (non-blocking)
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    await _mediator.Publish(new FarmersImportedEvent
                    {
                        ImportedFarmers = new List<ImportedFarmerData>
                        {
                            new ImportedFarmerData
                            {
                                FullName = newFarmer.FullName,
                                PhoneNumber = newFarmer.PhoneNumber!,
                                Email = newFarmer.Email,
                                Address = newFarmer.Address,
                                FarmCode = newFarmer.FarmCode,
                                TempPassword = TEMP_PASSWORD
                            }
                        },
                        ImportedAt = DateTime.UtcNow
                    }, cancellationToken);

                    _logger.LogInformation("Published email notification event for farmer {FarmerId}", newFarmer.Id);
                }

                var farmerDTOs = _mapper.Map<FarmerDTO>(newFarmer);
                return Result<FarmerDTO>.Success(farmerDTOs, "Tạo nông dân thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                   "Error occurred while creating farmer with phone: {PhoneNumber}",
                   request.PhoneNumber);
                return Result<FarmerDTO>.Failure(
                    $"Error creating farmer: {ex.Message}");
            }
        }
    }
}
