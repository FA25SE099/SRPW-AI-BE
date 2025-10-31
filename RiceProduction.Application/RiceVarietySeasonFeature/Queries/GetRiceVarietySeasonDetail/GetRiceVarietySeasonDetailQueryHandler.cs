using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Queries.GetRiceVarietySeasonDetail
{
    public class GetRiceVarietySeasonDetailQueryHandler : IRequestHandler<GetRiceVarietySeasonDetailQuery, Result<RiceVarietySeasonDetailResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetRiceVarietySeasonDetailQueryHandler> _logger;

        public GetRiceVarietySeasonDetailQueryHandler(IUnitOfWork unitOfWork, ILogger<GetRiceVarietySeasonDetailQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<RiceVarietySeasonDetailResponse>> Handle(GetRiceVarietySeasonDetailQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietySeason = await _unitOfWork.Repository<RiceVarietySeason>().FindAsync(
                    match: rvs => rvs.Id == request.RiceVarietySeasonId,
                    includeProperties: q => q
                        .Include(rvs => rvs.RiceVariety)
                            .ThenInclude(rv => rv.Category)
                        .Include(rvs => rvs.Season));

                if (riceVarietySeason == null)
                {
                    return Result<RiceVarietySeasonDetailResponse>.Failure(
                        $"Rice variety season association with ID {request.RiceVarietySeasonId} not found",
                        "RiceVarietySeasonNotFound");
                }

                var response = new RiceVarietySeasonDetailResponse
                {
                    Id = riceVarietySeason.Id,
                    RiceVarietyId = riceVarietySeason.RiceVarietyId,
                    RiceVarietyName = riceVarietySeason.RiceVariety.VarietyName,
                    CategoryId = riceVarietySeason.RiceVariety.CategoryId,
                    CategoryName = riceVarietySeason.RiceVariety.Category.CategoryName,
                    SeasonId = riceVarietySeason.SeasonId,
                    SeasonName = riceVarietySeason.Season.SeasonName,
                    SeasonStartDate = riceVarietySeason.Season.StartDate,
                    SeasonEndDate = riceVarietySeason.Season.EndDate,
                    GrowthDurationDays = riceVarietySeason.GrowthDurationDays,
                    ExpectedYieldPerHectare = riceVarietySeason.ExpectedYieldPerHectare,
                    OptimalPlantingStart = riceVarietySeason.OptimalPlantingStart,
                    OptimalPlantingEnd = riceVarietySeason.OptimalPlantingEnd,
                    RiskLevel = riceVarietySeason.RiskLevel,
                    SeasonalNotes = riceVarietySeason.SeasonalNotes,
                    IsRecommended = riceVarietySeason.IsRecommended,
                    CreatedAt = riceVarietySeason.CreatedAt,
                    LastModified = riceVarietySeason.LastModified
                };

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

