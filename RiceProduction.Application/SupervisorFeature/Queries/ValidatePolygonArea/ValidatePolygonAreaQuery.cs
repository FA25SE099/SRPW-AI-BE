using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;

namespace RiceProduction.Application.SupervisorFeature.Queries.ValidatePolygonArea;

public class ValidatePolygonAreaQuery : IRequest<Result<PolygonValidationResponse>>
{
    public Guid PlotId { get; set; }
    public string PolygonGeoJson { get; set; } = string.Empty;
    public decimal TolerancePercent { get; set; } = 10; // Default 10% tolerance
}

