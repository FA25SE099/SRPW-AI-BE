using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Queries.GetAllRiceVarietySeasons
{
    public class GetAllRiceVarietySeasonsQueryHandler : IRequestHandler<GetAllRiceVarietySeasonsQuery, Result<List<RiceVarietySeasonResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAllRiceVarietySeasonsQueryHandler> _logger;

        public GetAllRiceVarietySeasonsQueryHandler(IApplicationDbContext context, ILogger<GetAllRiceVarietySeasonsQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<RiceVarietySeasonResponse>>> Handle(GetAllRiceVarietySeasonsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.RiceVarietySeasons.AsNoTracking();

                if (request.RiceVarietyId.HasValue)
                {
                    query = query.Where(rvs => rvs.RiceVarietyId == request.RiceVarietyId.Value);
                }

                if (request.SeasonId.HasValue)
                {
                    query = query.Where(rvs => rvs.SeasonId == request.SeasonId.Value);
                }

                if (request.IsRecommended.HasValue)
                {
                    query = query.Where(rvs => rvs.IsRecommended == request.IsRecommended.Value);
                }

                var responses = await query
                    .OrderBy(rvs => rvs.RiceVariety.VarietyName)
                    .ThenBy(rvs => rvs.Season.SeasonName)
                    .Select(rvs => new RiceVarietySeasonResponse
                    {
                        Id = rvs.Id,
                        RiceVarietyId = rvs.RiceVarietyId,
                        RiceVarietyName = rvs.RiceVariety.VarietyName,
                        SeasonId = rvs.SeasonId,
                        SeasonName = rvs.Season.SeasonName,
                        GrowthDurationDays = rvs.GrowthDurationDays,
                        ExpectedYieldPerHectare = rvs.ExpectedYieldPerHectare,
                        OptimalPlantingStart = rvs.OptimalPlantingStart,
                        OptimalPlantingEnd = rvs.OptimalPlantingEnd,
                        RiskLevel = rvs.RiskLevel,
                        SeasonalNotes = rvs.SeasonalNotes,
                        IsRecommended = rvs.IsRecommended,
                        CreatedAt = rvs.CreatedAt
                    })
                    .ToListAsync(cancellationToken);

                return Result<List<RiceVarietySeasonResponse>>.Success(responses, "Successfully retrieved all rice variety season associations.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all rice variety season associations");
                return Result<List<RiceVarietySeasonResponse>>.Failure("An error occurred while retrieving rice variety season associations");
            }
        }
    }
}

