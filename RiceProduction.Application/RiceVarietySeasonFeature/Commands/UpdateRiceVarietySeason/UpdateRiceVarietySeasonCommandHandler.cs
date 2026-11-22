using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Commands.UpdateRiceVarietySeason
{
    public class UpdateRiceVarietySeasonCommandHandler : IRequestHandler<UpdateRiceVarietySeasonCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<UpdateRiceVarietySeasonCommandHandler> _logger;

        public UpdateRiceVarietySeasonCommandHandler(IApplicationDbContext context, ILogger<UpdateRiceVarietySeasonCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateRiceVarietySeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietySeason = await _context.RiceVarietySeasons
                    .FirstOrDefaultAsync(rvs => rvs.Id == request.RiceVarietySeasonId, cancellationToken);
                if (riceVarietySeason == null)
                {
                    return Result<Guid>.Failure($"Rice variety season association with ID {request.RiceVarietySeasonId} not found");
                }

                riceVarietySeason.GrowthDurationDays = request.GrowthDurationDays;
                riceVarietySeason.ExpectedYieldPerHectare = request.ExpectedYieldPerHectare;
                riceVarietySeason.OptimalPlantingStart = request.OptimalPlantingStart;
                riceVarietySeason.OptimalPlantingEnd = request.OptimalPlantingEnd;
                riceVarietySeason.RiskLevel = request.RiskLevel;
                riceVarietySeason.SeasonalNotes = request.SeasonalNotes;
                riceVarietySeason.IsRecommended = request.IsRecommended;

                _context.RiceVarietySeasons.Update(riceVarietySeason);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Updated rice variety season association with ID: {Id}", request.RiceVarietySeasonId);
                return Result<Guid>.Success(request.RiceVarietySeasonId, "Rice variety season association updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rice variety season association with ID: {Id}", request.RiceVarietySeasonId);
                return Result<Guid>.Failure("Failed to update rice variety season association");
            }
        }
    }
}

