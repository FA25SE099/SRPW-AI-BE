using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.CreateRiceVariety
{
    public class CreateRiceVarietyCommandHandler : IRequestHandler<CreateRiceVarietyCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateRiceVarietyCommandHandler> _logger;

        public CreateRiceVarietyCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateRiceVarietyCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateRiceVarietyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietyRepo = _unitOfWork.Repository<RiceVariety>();
                var categoryRepo = _unitOfWork.Repository<RiceVarietyCategory>();

                var category = await categoryRepo.FindAsync(c => c.Id == request.CategoryId);
                if (category == null)
                {
                    return Result<Guid>.Failure($"Category with ID {request.CategoryId} not found");
                }

                var duplicate = await riceVarietyRepo.FindAsync(v => v.VarietyName == request.VarietyName);
                if (duplicate != null)
                {
                    return Result<Guid>.Failure($"Rice variety with name '{request.VarietyName}' already exists");
                }

                var id = await riceVarietyRepo.GenerateNewGuid(Guid.NewGuid());
                var newVariety = new RiceVariety
                {
                    Id = id,
                    VarietyName = request.VarietyName,
                    CategoryId = request.CategoryId,
                    BaseGrowthDurationDays = request.BaseGrowthDurationDays,
                    BaseYieldPerHectare = request.BaseYieldPerHectare,
                    Description = request.Description,
                    Characteristics = request.Characteristics,
                    IsActive = request.IsActive
                };

                await riceVarietyRepo.AddAsync(newVariety);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Created rice variety with ID: {RiceVarietyId}", id);
                return Result<Guid>.Success(id, "Rice variety created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rice variety");
                return Result<Guid>.Failure("Failed to create rice variety");
            }
        }
    }
}

