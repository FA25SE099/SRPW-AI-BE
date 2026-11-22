using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Queries.GetAllSeasons
{
    public class GetAllSeasonsQueryHandler : IRequestHandler<GetAllSeasonsQuery, Result<List<SeasonResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAllSeasonsQueryHandler> _logger;

        public GetAllSeasonsQueryHandler(IApplicationDbContext context, ILogger<GetAllSeasonsQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<SeasonResponse>>> Handle(GetAllSeasonsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.Seasons.AsNoTracking();

                if (!string.IsNullOrEmpty(request.Search))
                {
                    query = query.Where(s =>
                        s.SeasonName.Contains(request.Search) ||
                        (s.SeasonType != null && s.SeasonType.Contains(request.Search)));
                }

                if (request.IsActive.HasValue)
                {
                    query = query.Where(s => s.IsActive == request.IsActive.Value);
                }

                var seasonResponses = await query
                    .OrderBy(s => s.SeasonName)
                    .Select(s => new SeasonResponse
                    {
                        Id = s.Id,
                        SeasonName = s.SeasonName,
                        StartDate = s.StartDate,
                        EndDate = s.EndDate,
                        SeasonType = s.SeasonType,
                        IsActive = s.IsActive,
                        CreatedAt = s.CreatedAt
                    })
                    .ToListAsync(cancellationToken);

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

