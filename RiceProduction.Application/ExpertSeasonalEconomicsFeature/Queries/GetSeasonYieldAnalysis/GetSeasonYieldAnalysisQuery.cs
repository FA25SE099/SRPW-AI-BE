using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics;

namespace RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetSeasonYieldAnalysis
{
    public class GetSeasonYieldAnalysisQuery : IRequest<Result<SeasonYieldAnalysisResponse>>, ICacheable
    {
        public Guid SeasonId { get; set; }
        public Guid? ClusterId { get; set; }
        public Guid? GroupId { get; set; }
        
        public bool BypassCache { get; init; } = false;
        public string CacheKey => $"SeasonYieldAnalysis:Season:{SeasonId}:Cluster:{ClusterId}:Group:{GroupId}";
        public int SlidingExpirationInMinutes => 45;
        public int AbsoluteExpirationInMinutes => 90;
    }
}

