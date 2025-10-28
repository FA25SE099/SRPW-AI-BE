using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.UpdateRiceVariety
{
    public class UpdateRiceVarietyCommandHandler : IRequestHandler<UpdateRiceVarietyCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateRiceVarietyCommandHandler> _logger;

        public UpdateRiceVarietyCommandHandler(IUnitOfWork unitOfWork, ILogger<UpdateRiceVarietyCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateRiceVarietyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietyRepo = _unitOfWork.Repository<RiceVariety>();
                var categoryRepo = _unitOfWork.Repository<RiceVarietyCategory>();

                var variety = await riceVarietyRepo.FindAsync(v => v.Id == request.RiceVarietyId);
                if (variety == null)
                {
                    return Result<Guid>.Failure($"Rice variety with ID {request.RiceVarietyId} not found");
                }

                var category = await categoryRepo.FindAsync(c => c.Id == request.CategoryId);
                if (category == null)
                {
                    return Result<Guid>.Failure($"Category with ID {request.CategoryId} not found");
                }

                variety.VarietyName = request.VarietyName;
                variety.CategoryId = request.CategoryId;
                variety.BaseGrowthDurationDays = request.BaseGrowthDurationDays;
                variety.BaseYieldPerHectare = request.BaseYieldPerHectare;
                variety.Description = request.Description;
                variety.Characteristics = request.Characteristics;
                variety.IsActive = request.IsActive;

                riceVarietyRepo.Update(variety);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Updated rice variety with ID: {RiceVarietyId}", request.RiceVarietyId);
                return Result<Guid>.Success(request.RiceVarietyId, "Rice variety updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rice variety with ID: {RiceVarietyId}", request.RiceVarietyId);
                return Result<Guid>.Failure("Failed to update rice variety");
            }
        }
    }
}

