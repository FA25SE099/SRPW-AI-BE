using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Commands.CreateRiceVarietySeason
{
    public class CreateRiceVarietySeasonCommandHandler : IRequestHandler<CreateRiceVarietySeasonCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateRiceVarietySeasonCommandHandler> _logger;

        public CreateRiceVarietySeasonCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateRiceVarietySeasonCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateRiceVarietySeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietySeasonRepo = _unitOfWork.Repository<RiceVarietySeason>();
                var riceVarietyRepo = _unitOfWork.Repository<RiceVariety>();
                var seasonRepo = _unitOfWork.Repository<Season>();

                var riceVariety = await riceVarietyRepo.FindAsync(rv => rv.Id == request.RiceVarietyId);
                if (riceVariety == null)
                {
                    return Result<Guid>.Failure($"Rice variety with ID {request.RiceVarietyId} not found");
                }

                var season = await seasonRepo.FindAsync(s => s.Id == request.SeasonId);
                if (season == null)
                {
                    return Result<Guid>.Failure($"Season with ID {request.SeasonId} not found");
                }

                var duplicate = await riceVarietySeasonRepo.FindAsync(rvs => 
                    rvs.RiceVarietyId == request.RiceVarietyId && rvs.SeasonId == request.SeasonId);
                if (duplicate != null)
                {
                    return Result<Guid>.Failure($"Rice variety is already associated with this season");
                }

                var id = await riceVarietySeasonRepo.GenerateNewGuid(Guid.NewGuid());
                var newRiceVarietySeason = new RiceVarietySeason
                {
                    Id = id,
                    RiceVarietyId = request.RiceVarietyId,
                    SeasonId = request.SeasonId,
                    GrowthDurationDays = request.GrowthDurationDays,
                    ExpectedYieldPerHectare = request.ExpectedYieldPerHectare,
                    OptimalPlantingStart = request.OptimalPlantingStart,
                    OptimalPlantingEnd = request.OptimalPlantingEnd,
                    RiskLevel = request.RiskLevel,
                    SeasonalNotes = request.SeasonalNotes,
                    IsRecommended = request.IsRecommended
                };

                await riceVarietySeasonRepo.AddAsync(newRiceVarietySeason);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Created rice variety season association with ID: {Id}", id);
                return Result<Guid>.Success(id, "Rice variety season association created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rice variety season association");
                return Result<Guid>.Failure("Failed to create rice variety season association");
            }
        }
    }
}

