using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.FarmerCultivationTaskFeature.Queries.GetFarmerCultivationTasks
{
    public class GetFarmerCultivationTasksQuery : IRequest<Result<List<FarmerCultivationTaskResponse>>>, ICacheable
    {
        public Guid FarmerId { get; set; }
        public Guid? SeasonId { get; set; }
        public Guid? PlotId { get; set; }
        public TaskStatus? Status { get; set; }
        public bool? IncludePastSeasons { get; set; } = false;
        public bool? IncludeCompleted { get; set; } = true;
        
        public bool BypassCache { get; init; } = false;
        public string CacheKey => $"FarmerCultivationTasks:Farmer:{FarmerId}:Season:{SeasonId}:Plot:{PlotId}:Status:{Status}:Past:{IncludePastSeasons}:Completed:{IncludeCompleted}";
        public int SlidingExpirationInMinutes => 15;
        public int AbsoluteExpirationInMinutes => 30;
    }
}

