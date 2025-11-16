using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterCurrentSeason;

public class GetClusterCurrentSeasonQuery : IRequest<Result<ClusterCurrentSeasonResponse>>
{
    public Guid ClusterId { get; set; }
}

