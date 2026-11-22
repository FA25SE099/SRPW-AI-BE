using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Queries.GetRiceVarietySeasonDetail
{
    public class GetRiceVarietySeasonDetailQueryHandler : IRequestHandler<GetRiceVarietySeasonDetailQuery, Result<RiceVarietySeasonDetailResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetRiceVarietySeasonDetailQueryHandler> _logger;

        public GetRiceVarietySeasonDetailQueryHandler(IApplicationDbContext context, ILogger<GetRiceVarietySeasonDetailQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<RiceVarietySeasonDetailResponse>> Handle(GetRiceVarietySeasonDetailQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _context.RiceVarietySeasons
                    .AsNoTracking()
                    .Where(rvs => rvs.Id == request.RiceVarietySeasonId)
                    .Select(rvs => new RiceVarietySeasonDetailResponse
                    {
                        Id = rvs.Id,
                        RiceVarietyId = rvs.RiceVarietyId,
                        RiceVarietyName = rvs.RiceVariety.VarietyName,
                        CategoryId = rvs.RiceVariety.CategoryId,
                        CategoryName = rvs.RiceVariety.Category.CategoryName,
                        SeasonId = rvs.SeasonId,
                        SeasonName = rvs.Season.SeasonName,
                        SeasonStartDate = rvs.Season.StartDate,
                        SeasonEndDate = rvs.Season.EndDate,
                        GrowthDurationDays = rvs.GrowthDurationDays,
                        ExpectedYieldPerHectare = rvs.ExpectedYieldPerHectare,
                        OptimalPlantingStart = rvs.OptimalPlantingStart,
                        OptimalPlantingEnd = rvs.OptimalPlantingEnd,
                        RiskLevel = rvs.RiskLevel,
                        SeasonalNotes = rvs.SeasonalNotes,
                        IsRecommended = rvs.IsRecommended,
                        CreatedAt = rvs.CreatedAt,
                        LastModified = rvs.LastModified
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (response == null)
                {
                    return Result<RiceVarietySeasonDetailResponse>.Failure(
                        $"Rice variety season association with ID {request.RiceVarietySeasonId} not found",
                        "RiceVarietySeasonNotFound");
                }

                return Result<RiceVarietySeasonDetailResponse>.Success(response, "Successfully retrieved rice variety season association details.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rice variety season association detail with ID: {Id}", request.RiceVarietySeasonId);
                return Result<RiceVarietySeasonDetailResponse>.Failure("An error occurred while retrieving rice variety season association details");
            }
        }
    }
}

