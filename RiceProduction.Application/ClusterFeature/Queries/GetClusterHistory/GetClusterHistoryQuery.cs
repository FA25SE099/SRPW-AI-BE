using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterHistory;

public class GetClusterHistoryQuery : IRequest<Result<ClusterHistoryResponse>>
{
    public Guid ClusterId { get; set; }
    public Guid? SeasonId { get; set; }
    public int? Year { get; set; }
    public int? Limit { get; set; } = 5;
}

