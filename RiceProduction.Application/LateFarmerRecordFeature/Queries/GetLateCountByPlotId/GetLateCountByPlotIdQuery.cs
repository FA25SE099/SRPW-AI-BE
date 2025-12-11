using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateCountByPlotId;

public class GetLateCountByPlotIdQuery : IRequest<Result<PlotLateCountDTO>>
{
    public Guid PlotId { get; set; }
}
