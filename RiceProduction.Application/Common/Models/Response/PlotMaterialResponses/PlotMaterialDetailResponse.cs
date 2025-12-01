namespace RiceProduction.Application.Common.Models.Response.PlotMaterialResponses;

public class PlotMaterialDetailResponse
{
    public Guid PlotId { get; set; }
    public decimal PlotArea { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public List<PlotMaterialItemResponse> Materials { get; set; } = new();
    public decimal TotalEstimatedCost { get; set; }
}

