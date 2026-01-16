namespace RiceProduction.Application.SeasonFeature.Queries.GetAvailableRiceVarietiesForSeason;

public class RiceVarietySeasonDto
{
    public Guid RiceVarietyId { get; set; }
    public string VarietyName { get; set; } = string.Empty;
    public int GrowthDurationDays { get; set; }
    public decimal? ExpectedYieldPerHectare { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
    public string? SeasonalNotes { get; set; }
    public DateTime? OptimalPlantingStart { get; set; }
    public DateTime? OptimalPlantingEnd { get; set; }
}



