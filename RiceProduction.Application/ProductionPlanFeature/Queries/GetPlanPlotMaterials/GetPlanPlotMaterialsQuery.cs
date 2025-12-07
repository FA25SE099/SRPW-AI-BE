using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotMaterialResponses;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanPlotMaterials;

public class GetPlanPlotMaterialsQuery : IRequest<Result<PlanPlotMaterialsResponse>>
{
    public Guid PlanId { get; set; }
}

