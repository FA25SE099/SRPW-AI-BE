using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SeasonFeature.Queries.GetAvailableRiceVarietiesForSeason;

public class GetAvailableRiceVarietiesForSeasonQueryHandler 
    : IRequestHandler<GetAvailableRiceVarietiesForSeasonQuery, Result<List<RiceVarietySeasonDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetAvailableRiceVarietiesForSeasonQueryHandler> _logger;

    public GetAvailableRiceVarietiesForSeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetAvailableRiceVarietiesForSeasonQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<RiceVarietySeasonDto>>> Handle(
        GetAvailableRiceVarietiesForSeasonQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if season exists
            var season = await _unitOfWork.Repository<Season>()
                .FindAsync(s => s.Id == request.SeasonId);

            if (season == null)
            {
                return Result<List<RiceVarietySeasonDto>>.Failure("Season not found");
            }

            // Get rice varieties suitable for this season
            var query = _unitOfWork.Repository<RiceVarietySeason>()
                .GetQueryable()
                .Include(rvs => rvs.RiceVariety)
                .Where(rvs => rvs.SeasonId == request.SeasonId);

            // Filter by recommended if requested
            if (request.OnlyRecommended)
            {
                query = query.Where(rvs => rvs.IsRecommended);
            }

            var varieties = await query
                .OrderBy(rvs => rvs.RiskLevel)
                .ThenByDescending(rvs => rvs.ExpectedYieldPerHectare)
                .ToListAsync(cancellationToken);

            // Map to DTO
            var result = varieties.Select(rvs => new RiceVarietySeasonDto
            {
                RiceVarietyId = rvs.RiceVarietyId,
                VarietyName = rvs.RiceVariety.VarietyName,
                GrowthDurationDays = (int)rvs.RiceVariety.BaseGrowthDurationDays,
                ExpectedYieldPerHectare = rvs.ExpectedYieldPerHectare,
                RiskLevel = rvs.RiskLevel.ToString(),
                IsRecommended = rvs.IsRecommended,
                SeasonalNotes = rvs.SeasonalNotes,
                OptimalPlantingStart = null,
                OptimalPlantingEnd = null
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} rice varieties for season {SeasonId}", 
                result.Count, request.SeasonId);

            return Result<List<RiceVarietySeasonDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error retrieving rice varieties for season {SeasonId}", 
                request.SeasonId);
            return Result<List<RiceVarietySeasonDto>>.Failure(
                "An error occurred while retrieving rice varieties");
        }
    }
}

