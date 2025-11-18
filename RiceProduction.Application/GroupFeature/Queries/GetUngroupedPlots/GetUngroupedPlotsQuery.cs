using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;

namespace RiceProduction.Application.GroupFeature.Queries.GetUngroupedPlots;

public class GetUngroupedPlotsQuery : IRequest<Result<UngroupedPlotsResponse>>
{
    public Guid ClusterId { get; set; }
    public Guid SeasonId { get; set; }
    public int Year { get; set; }
}

