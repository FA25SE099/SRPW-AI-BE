using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarietyCategories
{
    public class GetAllRiceVarietyCategoriesQuery : IRequest<Result<List<RiceVarietyCategoryResponse>>>
    {
        public bool? IsActive { get; set; }
    }
}

