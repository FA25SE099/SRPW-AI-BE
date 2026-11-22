using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Commands.CreateRiceVarietySeason
{
    public class CreateRiceVarietySeasonCommandHandler : IRequestHandler<CreateRiceVarietySeasonCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<CreateRiceVarietySeasonCommandHandler> _logger;

        public CreateRiceVarietySeasonCommandHandler(IApplicationDbContext context, ILogger<CreateRiceVarietySeasonCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateRiceVarietySeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietyExists = await _context.RiceVarieties
                    .AsNoTracking()
                    .AnyAsync(rv => rv.Id == request.RiceVarietyId, cancellationToken);
                if (!riceVarietyExists)
                {
                    return Result<Guid>.Failure($"Rice variety with ID {request.RiceVarietyId} not found");
                }

                var seasonExists = await _context.Seasons
                    .AsNoTracking()
                    .AnyAsync(s => s.Id == request.SeasonId, cancellationToken);
                if (!seasonExists)
                {
                    return Result<Guid>.Failure($"Season with ID {request.SeasonId} not found");
                }

                var duplicateExists = await _context.RiceVarietySeasons
                    .AsNoTracking()
                    .AnyAsync(rvs =>
                        rvs.RiceVarietyId == request.RiceVarietyId && rvs.SeasonId == request.SeasonId,
                        cancellationToken);
                if (duplicateExists)
                {
                    return Result<Guid>.Failure($"Rice variety is already associated with this season");
                }

                var id = Guid.NewGuid();
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

                _context.RiceVarietySeasons.Add(newRiceVarietySeason);
                await _context.SaveChangesAsync(cancellationToken);

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

