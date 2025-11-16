using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;

namespace RiceProduction.Application.GroupFeature.Queries.PreviewGroups;

public class PreviewGroupsQuery : IRequest<Result<PreviewGroupsResponse>>
{
    public Guid ClusterId { get; set; }
    public Guid SeasonId { get; set; }
    public int Year { get; set; }
    public double? ProximityThreshold { get; set; }
    public int? PlantingDateTolerance { get; set; }
    public decimal? MinGroupArea { get; set; }
    public decimal? MaxGroupArea { get; set; }
    public int? MinPlotsPerGroup { get; set; }
    public int? MaxPlotsPerGroup { get; set; }
}

