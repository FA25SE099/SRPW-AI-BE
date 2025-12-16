using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.CheckPlotPolygonEditable;

public class CheckPlotPolygonEditableQuery : IRequest<Result<CheckPlotPolygonEditableResponse>>
{
    public Guid PlotId { get; set; }
    public Guid YearSeasonId { get; set; }
}

public class CheckPlotPolygonEditableResponse
{
    public bool IsEditable { get; set; }
    public string? Reason { get; set; }
}

