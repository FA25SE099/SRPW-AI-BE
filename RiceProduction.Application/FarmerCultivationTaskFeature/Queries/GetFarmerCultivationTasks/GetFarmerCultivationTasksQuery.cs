using RiceProduction.Application.Common.Models;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.FarmerCultivationTaskFeature.Queries.GetFarmerCultivationTasks
{
    public class GetFarmerCultivationTasksQuery : IRequest<Result<List<FarmerCultivationTaskResponse>>>
    {
        public Guid FarmerId { get; set; }
        public Guid? SeasonId { get; set; }
        public Guid? PlotId { get; set; }
        public TaskStatus? Status { get; set; }
        public bool? IncludePastSeasons { get; set; } = false;
        public bool? IncludeCompleted { get; set; } = true;
    }
}

