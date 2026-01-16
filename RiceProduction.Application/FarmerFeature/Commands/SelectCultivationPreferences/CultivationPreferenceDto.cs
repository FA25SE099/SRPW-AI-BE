namespace RiceProduction.Application.FarmerFeature.Commands.SelectCultivationPreferences;

public class CultivationPreferenceDto
{
    public Guid PlotCultivationId { get; set; }
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public Guid YearSeasonId { get; set; }
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public DateTime PlantingDate { get; set; }
    public DateTime? EstimatedHarvestDate { get; set; }
    public int? GrowthDurationDays { get; set; }
    public decimal? ExpectedYield { get; set; }
    public DateTime SelectionDate { get; set; }
    public string Status { get; set; } = string.Empty;
}



