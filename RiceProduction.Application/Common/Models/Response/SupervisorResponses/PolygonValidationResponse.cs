namespace RiceProduction.Application.Common.Models.Response.SupervisorResponses;

public class PolygonValidationResponse
{
    public bool IsValid { get; set; }
    public decimal DrawnAreaHa { get; set; }
    public decimal PlotAreaHa { get; set; }
    public decimal DifferencePercent { get; set; }
    public decimal TolerancePercent { get; set; }
    public string? Message { get; set; }
}

