namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmerCultivationSelections;

public class FarmerCultivationSelectionsDto
{
    public Guid YearSeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime? SelectionDeadline { get; set; }
    public int DaysUntilDeadline { get; set; }
    public int TotalPlots { get; set; }
    public int ConfirmedPlots { get; set; }
    public int PendingPlots { get; set; }
    public List<PlotCultivationSelectionDto> Selections { get; set; } = new();
}

public class PlotCultivationSelectionDto
{
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }
    public bool IsConfirmed { get; set; }
    public Guid? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime? PlantingDate { get; set; }
    public DateTime? EstimatedHarvestDate { get; set; }
    public decimal? ExpectedYield { get; set; }
    public DateTime? SelectionDate { get; set; }
}



