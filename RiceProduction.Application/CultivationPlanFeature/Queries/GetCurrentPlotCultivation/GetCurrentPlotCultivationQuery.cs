using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCurrentPlotCultivation;

public class GetCurrentPlotCultivationQuery : IRequest<Result<CurrentPlotCultivationDetailResponse>>
{
    public Guid PlotId { get; set; }
}
