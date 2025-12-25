using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetCurrentPlotCultivation;
using System;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetPlotCultivationByGroupAndPlot;

public class GetPlotCultivationByGroupAndPlotQuery : IRequest<Result<CurrentPlotCultivationDetailResponse>>
{
    public Guid PlotId { get; set; }
    public Guid GroupId { get; set; }
    
    /// <summary>
    /// Optional: Specific version ID to query. If null, returns the latest version (highest VersionOrder).
    /// </summary>
    public Guid? VersionId { get; set; }
}
