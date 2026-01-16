namespace RiceProduction.Application.PlotFeature.Queries.PreviewPlotImport;

/// <summary>
/// Preview data for plot import showing valid and invalid rows
/// </summary>
public class PlotImportPreviewDto
{
    public int TotalRows { get; set; }
    public int ValidRowsCount { get; set; }
    public int InvalidRowsCount { get; set; }
    public int SkippedRowsCount { get; set; }
    public int DuplicateRowsCount { get; set; }
    public List<PlotImportPreviewRow> ValidRows { get; set; } = new();
    public List<PlotImportPreviewRow> InvalidRows { get; set; } = new();
    public List<string> GeneralErrors { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();
}

/// <summary>
/// Preview data for a single row in the import
/// </summary>
public class PlotImportPreviewRow
{
    public int RowNumber { get; set; }
    public string FarmCode { get; set; } = string.Empty;
    public string FarmerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public decimal? Area { get; set; }
    public string? SoilType { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime? PlantingDate { get; set; }
    public string? SeasonName { get; set; }
    public int? Year { get; set; }
    public bool HasPolygon { get; set; }
    public bool IsDuplicate { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string Status { get; set; } = string.Empty; // "Valid", "Invalid", "Warning", "Duplicate"
    public string? PlotStatus { get; set; } // "Active" or "PendingPolygon"
    public string? WillCreatePlotCultivation { get; set; }
    public string? SeasonYear { get; set; }
    
    /// <summary>
    /// Plot boundary polygon in WKT format (returned so frontend doesn't need to read Excel again)
    /// Example: POLYGON((105.123 10.456, 105.124 10.457, ...))
    /// </summary>
    public string? BoundaryWKT { get; set; }
    
    /// <summary>
    /// Plot center point in WKT format (returned so frontend doesn't need to read Excel again)
    /// Example: POINT(105.123 10.456)
    /// </summary>
    public string? CoordinateWKT { get; set; }
}
