namespace RiceProduction.Application.FarmLogFeature.Queries;
public class FarmLogDetailResponse
{
    public Guid FarmLogId { get; set; }
    public string CultivationTaskName { get; set; } = string.Empty;
    public string PlotName { get; set; } = string.Empty;
    public DateTime LoggedDate { get; set; }
    public string? WorkDescription { get; set; }
    public int CompletionPercentage { get; set; }
    public decimal? ActualAreaCovered { get; set; }
    public decimal? ServiceCost { get; set; }
    public string? ServiceNotes { get; set; }
    public string[]? PhotoUrls { get; set; }
    public string? WeatherConditions { get; set; }
    public string? InterruptionReason { get; set; }
    
    public List<FarmLogMaterialRecord> MaterialsUsed { get; set; } = new();
}

public class FarmLogMaterialRecord
{
    public string MaterialName { get; set; } = string.Empty;
    public decimal ActualQuantityUsed { get; set; }
    public decimal ActualCost { get; set; }
    public string? Notes { get; set; }
}