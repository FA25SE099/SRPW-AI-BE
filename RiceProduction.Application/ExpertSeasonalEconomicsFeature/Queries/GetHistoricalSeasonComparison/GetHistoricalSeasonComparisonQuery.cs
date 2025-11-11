using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics;

namespace RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetHistoricalSeasonComparison
{
    public class GetHistoricalSeasonComparisonQuery : IRequest<Result<HistoricalSeasonComparisonResponse>>, ICacheable
    {
        public List<Guid> SeasonIds { get; set; } = new List<Guid>();
        public Guid? ClusterId { get; set; }
        public int? Year { get; set; }
        public int? Limit { get; set; } = 5;
        
        public bool BypassCache { get; init; } = false;
        public string CacheKey => $"SeasonComparison:Seasons:{string.Join("-", SeasonIds.OrderBy(x => x))}:Cluster:{ClusterId}:Year:{Year}:Limit:{Limit}";
        public int SlidingExpirationInMinutes => 60;
        public int AbsoluteExpirationInMinutes => 120;
    }
}

