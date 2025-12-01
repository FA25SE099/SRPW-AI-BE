namespace RiceProduction.Application.Common.Models.Response.PlotMaterialResponses;

public class PlanPlotMaterialsResponse
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public List<PlotMaterialDetailResponse> Plots { get; set; } = new();
}

