namespace RiceProduction.Application.Common.Models.Response.ReportResponses;

/// <summary>
/// Report details with emergency tasks and material costs
/// </summary>
public class ReportWithEmergencyMaterialsResponse
{
    // Basic report information
    public Guid ReportId { get; set; }
    public Guid? PlotId { get; set; }
    public string? PlotName { get; set; }
    public decimal? PlotArea { get; set; }
    public Guid? CultivationPlanId { get; set; }
    public string? CultivationPlanName { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public string? ReportedByRole { get; set; }
    public DateTime ReportedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string>? Images { get; set; }
    public string? Coordinates { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? FarmerName { get; set; }
    public string? ClusterName { get; set; }

    // Emergency tasks and materials
    public List<EmergencyTaskResponse> EmergencyTasks { get; set; } = new();
    public decimal TotalMaterialCost { get; set; }
    public int EmergencyTaskCount { get; set; }
    public List<string> PriceWarnings { get; set; } = new();
}
