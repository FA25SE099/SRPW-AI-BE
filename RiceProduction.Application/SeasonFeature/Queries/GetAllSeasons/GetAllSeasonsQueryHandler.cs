using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Application.SeasonFeature.Queries.GetAllSeasons
{
    public class GetAllSeasonsQueryHandler : IRequestHandler<GetAllSeasonsQuery, Result<List<SeasonResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetAllSeasonsQueryHandler> _logger;

        public GetAllSeasonsQueryHandler(IUnitOfWork unitOfWork, ILogger<GetAllSeasonsQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<List<SeasonResponse>>> Handle(GetAllSeasonsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                Expression<Func<Season, bool>> filter = s =>
                    (string.IsNullOrEmpty(request.Search) || s.SeasonName.Contains(request.Search) || (s.SeasonType != null && s.SeasonType.Contains(request.Search))) &&
                    (request.IsActive == null || s.IsActive == request.IsActive.Value);

                var seasons = await _unitOfWork.Repository<Season>().ListAsync(
                    filter: filter,
                    orderBy: q => q.OrderBy(s => s.SeasonName));

                var seasonResponses = seasons.Select(s => new SeasonResponse
                {
                    Id = s.Id,
                    SeasonName = s.SeasonName,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    SeasonType = s.SeasonType,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt
                }).ToList();

                return Result<List<SeasonResponse>>.Success(seasonResponses, "Successfully retrieved all seasons.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all seasons");
                return Result<List<SeasonResponse>>.Failure("An error occurred while retrieving seasons");
            }
        }
    }
}

