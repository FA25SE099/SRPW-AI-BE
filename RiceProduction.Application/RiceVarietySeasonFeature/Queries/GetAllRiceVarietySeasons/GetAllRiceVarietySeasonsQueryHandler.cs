using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Queries.GetAllRiceVarietySeasons
{
    public class GetAllRiceVarietySeasonsQueryHandler : IRequestHandler<GetAllRiceVarietySeasonsQuery, Result<List<RiceVarietySeasonResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetAllRiceVarietySeasonsQueryHandler> _logger;

        public GetAllRiceVarietySeasonsQueryHandler(IUnitOfWork unitOfWork, ILogger<GetAllRiceVarietySeasonsQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<List<RiceVarietySeasonResponse>>> Handle(GetAllRiceVarietySeasonsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                Expression<Func<RiceVarietySeason, bool>> filter = rvs =>
                    (request.RiceVarietyId == null || rvs.RiceVarietyId == request.RiceVarietyId.Value) &&
                    (request.SeasonId == null || rvs.SeasonId == request.SeasonId.Value) &&
                    (request.IsRecommended == null || rvs.IsRecommended == request.IsRecommended.Value);

                var riceVarietySeasons = await _unitOfWork.Repository<RiceVarietySeason>().ListAsync(
                    filter: filter,
                    orderBy: q => q.OrderBy(rvs => rvs.RiceVariety.VarietyName).ThenBy(rvs => rvs.Season.SeasonName),
                    includeProperties: q => q
                        .Include(rvs => rvs.RiceVariety)
                        .Include(rvs => rvs.Season));

                var responses = riceVarietySeasons.Select(rvs => new RiceVarietySeasonResponse
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
                }).ToList();

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

