using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Command.UpdateFarmer
{
    public class UpdateFarmerCommandHandler : IRequestHandler<UpdateFarmerCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateFarmerCommandHandler> _logger;

        public UpdateFarmerCommandHandler(
            UserManager<ApplicationUser> userManager,
            IUnitOfWork unitOfWork,
            ILogger<UpdateFarmerCommandHandler> logger)
        {
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateFarmerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.FullName))
                {
                    return Result<Guid>.Failure("Full name is required");
                }

                // Get existing farmer
                var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(request.FarmerId, cancellationToken);
                if (farmer == null)
                {
                    return Result<Guid>.Failure($"Farmer with ID '{request.FarmerId}' not found");
                }

                // Update farmer information
                farmer.FullName = request.FullName;
                farmer.Address = request.Address;
                farmer.FarmCode = request.FarmCode;

                // Update in Identity
                var updateResult = await _userManager.UpdateAsync(farmer);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to update farmer: {Errors}", errors);
                    return Result<Guid>.Failure($"Failed to update farmer: {errors}");
                }

                _logger.LogInformation("Updated farmer with ID: {FarmerId}, Name: {FullName}", farmer.Id, farmer.FullName);

                return Result<Guid>.Success(farmer.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating farmer with ID: {FarmerId}", request.FarmerId);
                return Result<Guid>.Failure($"Error updating farmer: {ex.Message}");
            }
        }
    }
}

