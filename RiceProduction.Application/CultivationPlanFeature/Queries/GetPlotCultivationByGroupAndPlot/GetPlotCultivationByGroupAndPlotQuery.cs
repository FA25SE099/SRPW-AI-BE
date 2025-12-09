using RiceProduction.Application.Common.Models;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetCurrentPlotCultivation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetPlotCultivationByGroupAndPlot;

public class GetPlotCultivationByGroupAndPlotQuery : IRequest<Result<CurrentPlotCultivationDetailResponse>>
{
    public Guid PlotId { get; set; }
    public Guid GroupId { get; set; }
}
