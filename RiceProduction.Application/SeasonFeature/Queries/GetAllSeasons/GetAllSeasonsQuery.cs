using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Queries.GetAllSeasons
{
    public class GetAllSeasonsQuery : IRequest<Result<List<SeasonResponse>>>, ICacheable
    {
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        
        public bool BypassCache { get; init; } = false;
        public string CacheKey => $"Seasons:Search:{Search}:Active:{IsActive}";
        public int SlidingExpirationInMinutes => 60;
        public int AbsoluteExpirationInMinutes => 120;
    }

    public class SeasonResponse
    {
        public Guid Id { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string? SeasonType { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}

