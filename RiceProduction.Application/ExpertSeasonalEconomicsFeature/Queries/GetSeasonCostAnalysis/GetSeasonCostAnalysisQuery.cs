using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics;

namespace RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetSeasonCostAnalysis
{
    public class GetSeasonCostAnalysisQuery : IRequest<Result<SeasonCostAnalysisResponse>>, ICacheable
    {
        public Guid SeasonId { get; set; }
        public Guid? ClusterId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? RiceVarietyId { get; set; }
        
        public bool BypassCache { get; init; } = false;
        public string CacheKey => $"SeasonCostAnalysis:Season:{SeasonId}:Cluster:{ClusterId}:Group:{GroupId}:Variety:{RiceVarietyId}";
        public int SlidingExpirationInMinutes => 30;
        public int AbsoluteExpirationInMinutes => 60;
    }
}

