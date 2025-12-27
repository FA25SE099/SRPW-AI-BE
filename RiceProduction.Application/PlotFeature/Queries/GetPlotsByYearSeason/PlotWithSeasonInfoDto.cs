using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.GetPlotsByYearSeason;

/// <summary>
/// DTO for plot with season-specific cultivation information
/// </summary>
public class PlotWithSeasonInfoDto
{
    // Plot basic info
    public Guid PlotId { get; set; }
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? FarmerPhoneNumber { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public decimal Area { get; set; }
    public string? SoilType { get; set; }
    public PlotStatus PlotStatus { get; set; }
    public string? BoundaryGeoJson { get; set; }
    public string? CoordinateGeoJson { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    
    // YearSeason info
    public Guid YearSeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool AllowFarmerSelection { get; set; }
    public Guid? YearSeasonRiceVarietyId { get; set; }
    public string? YearSeasonRiceVarietyName { get; set; }
    
    // PlotCultivation info (nullable if farmer hasn't made selection)
    public Guid? PlotCultivationId { get; set; }
    public Guid? SelectedRiceVarietyId { get; set; }
    public string? SelectedRiceVarietyName { get; set; }
    public DateTime? SelectedPlantingDate { get; set; }
    public CultivationStatus? CultivationStatus { get; set; }
    public bool? IsFarmerConfirmed { get; set; }
    public DateTime? FarmerSelectionDate { get; set; }
    public string? FarmerSelectionNotes { get; set; }
    
    // Computed fields
    public bool HasMadeSelection { get; set; }
    public string SelectionStatusMessage { get; set; } = string.Empty;
}

