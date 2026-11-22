using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarieties;

public class GetAllRiceVarietiesQueryHandler : 
    IRequestHandler<GetAllRiceVarietiesQuery, Result<List<RiceVarietyResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllRiceVarietiesQueryHandler> _logger;

    public GetAllRiceVarietiesQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllRiceVarietiesQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<List<RiceVarietyResponse>>> Handle(GetAllRiceVarietiesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.RiceVarieties
                .AsNoTracking()
                .Include(v => v.Category)
                .Include(v => v.RiceVarietySeasons)
                    .ThenInclude(rvs => rvs.Season)
                .AsQueryable();

            if (!string.IsNullOrEmpty(request.Search))
            {
                query = query.Where(v =>
                    v.VarietyName.Contains(request.Search) ||
                    (v.Characteristics != null && v.Characteristics.Contains(request.Search)));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(v => v.IsActive == request.IsActive.Value);
            }

            if (request.CategoryId.HasValue)
            {
                query = query.Where(v => v.CategoryId == request.CategoryId.Value);
            }

            var riceVarieties = await query
                .OrderBy(v => v.VarietyName)
                .ToListAsync(cancellationToken);

            var varietyResponses = riceVarieties
                .Select(v => new RiceVarietyResponse
                {
                    Id = v.Id,
                    VarietyName = v.VarietyName,
                    CategoryId = v.CategoryId,
                    CategoryName = v.Category.CategoryName,
                    BaseGrowthDurationDays = v.BaseGrowthDurationDays,
                    BaseYieldPerHectare = v.BaseYieldPerHectare,
                    Description = v.Description,
                    Characteristics = v.Characteristics,
                    IsActive = v.IsActive,
                    AssociatedSeasons = v.RiceVarietySeasons.Select(rvs => new RiceVarietySeasonInfo
                    {
                        SeasonId = rvs.SeasonId,
                        SeasonName = rvs.Season.SeasonName,
                        StartDate = rvs.Season.StartDate,
                        EndDate = rvs.Season.EndDate,
                        GrowthDurationDays = rvs.GrowthDurationDays,
                        ExpectedYieldPerHectare = rvs.ExpectedYieldPerHectare,
                        OptimalPlantingStart = rvs.OptimalPlantingStart,
                        OptimalPlantingEnd = rvs.OptimalPlantingEnd,
                        RiskLevel = rvs.RiskLevel,
                        IsRecommended = rvs.IsRecommended
                    }).ToList()
                })
                .ToList();

            return Result<List<RiceVarietyResponse>>
                .Success(varietyResponses, "Successfully retrieved all rice varieties.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all rice varieties (non-paged)");

            return Result<List<RiceVarietyResponse>>
                .Failure("An error occurred while retrieving rice varieties", "GetAllRiceVarieties failed.");
        }
    }
}