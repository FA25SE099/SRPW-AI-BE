using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarietyCategories
{
    public class GetAllRiceVarietyCategoriesQueryHandler :
        IRequestHandler<GetAllRiceVarietyCategoriesQuery, Result<List<RiceVarietyCategoryResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAllRiceVarietyCategoriesQueryHandler> _logger;

        public GetAllRiceVarietyCategoriesQueryHandler(
            IApplicationDbContext context,
            ILogger<GetAllRiceVarietyCategoriesQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<RiceVarietyCategoryResponse>>> Handle(
            GetAllRiceVarietyCategoriesQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.RiceVarietyCategories.AsNoTracking();

                if (request.IsActive.HasValue)
                {
                    query = query.Where(c => c.IsActive == request.IsActive.Value);
                }

                var response = await query
                    .OrderBy(c => c.CategoryCode)
                    .Select(c => new RiceVarietyCategoryResponse
                    {
                        Id = c.Id,
                        CategoryName = c.CategoryName,
                        CategoryCode = c.CategoryCode,
                        Description = c.Description,
                        MinGrowthDays = c.MinGrowthDays,
                        MaxGrowthDays = c.MaxGrowthDays,
                        IsActive = c.IsActive
                    })
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Retrieved {Count} rice variety categories", response.Count);

                return Result<List<RiceVarietyCategoryResponse>>.Success(
                    response,
                    "Successfully retrieved rice variety categories");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving rice variety categories");
                return Result<List<RiceVarietyCategoryResponse>>.Failure(
                    "An error occurred while retrieving rice variety categories");
            }
        }
    }
}

