using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterAvailableSeasons;

public class GetClusterAvailableSeasonsQuery : IRequest<Result<ClusterSeasonsResponse>>
{
    public Guid ClusterId { get; set; }
    public bool IncludeEmpty { get; set; } = true;
    public int? Limit { get; set; }
}

