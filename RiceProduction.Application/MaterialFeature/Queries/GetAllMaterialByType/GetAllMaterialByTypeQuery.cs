using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType
{
    public class GetAllMaterialByTypeQuery : IRequest<PagedResult<List<MaterialResponse>>>, ICacheable
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public MaterialType MaterialType { get; set; }
        
        public bool BypassCache { get; init; } = false;
        public string CacheKey => $"Materials:Type:{MaterialType}:Page:{CurrentPage}:Size:{PageSize}";
        public int SlidingExpirationInMinutes => 30;
        public int AbsoluteExpirationInMinutes => 60;
    }
}
