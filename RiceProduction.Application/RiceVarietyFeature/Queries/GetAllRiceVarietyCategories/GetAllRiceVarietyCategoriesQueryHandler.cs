using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarietyCategories
{
    public class GetAllRiceVarietyCategoriesQueryHandler :
        IRequestHandler<GetAllRiceVarietyCategoriesQuery, Result<List<RiceVarietyCategoryResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetAllRiceVarietyCategoriesQueryHandler> _logger;

        public GetAllRiceVarietyCategoriesQueryHandler(
            IUnitOfWork unitOfWork,
            ILogger<GetAllRiceVarietyCategoriesQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<List<RiceVarietyCategoryResponse>>> Handle(
            GetAllRiceVarietyCategoriesQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var categories = await _unitOfWork.Repository<RiceVarietyCategory>().ListAsync(
                    filter: c => request.IsActive == null || c.IsActive == request.IsActive.Value,
                    orderBy: q => q.OrderBy(c => c.CategoryCode));

                var response = categories.Select(c => new RiceVarietyCategoryResponse
                {
                    Id = c.Id,
                    CategoryName = c.CategoryName,
                    CategoryCode = c.CategoryCode,
                    Description = c.Description,
                    MinGrowthDays = c.MinGrowthDays,
                    MaxGrowthDays = c.MaxGrowthDays,
                    IsActive = c.IsActive
                }).ToList();

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

