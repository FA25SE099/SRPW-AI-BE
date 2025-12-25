using System;

namespace RiceProduction.Application.Common.Models.Request.PlotRequest;

/// <summary>
/// DTO for importing plots with dynamic rows based on farmer's NumberOfPlots
/// One row per plot, multiple rows per farmer
/// </summary>
public class PlotImportRow
{
    /// <summary>
    /// Unique farm code to identify the farmer
    /// </summary>
    public string FarmCode { get; set; }
    
    /// <summary>
    /// Farmer's full name (pre-filled in template for reference)
    /// </summary>
    public string FarmerName { get; set; }
    
    /// <summary>
    /// Farmer's phone number (pre-filled in template for reference)
    /// </summary>
    public string PhoneNumber { get; set; }
    
    /// <summary>
    /// Plot number for this farmer (1, 2, 3, 4...)
    /// </summary>
    public int PlotNumber { get; set; }
    
    /// <summary>
    /// Land parcel number (Số thửa)
    /// </summary>
    public int? SoThua { get; set; }
    
    /// <summary>
    /// Land map sheet number (Số tờ)
    /// </summary>
    public int? SoTo { get; set; }
    
    /// <summary>
    /// Plot area in square meters
    /// </summary>
    public decimal? Area { get; set; }
    
    /// <summary>
    /// Soil type (e.g., Clay, Sandy, Loam)
    /// </summary>
    public string? SoilType { get; set; }
    
    /// <summary>
    /// Rice variety name (refer to Rice_Varieties sheet)
    /// Will be used to create PlotCultivation for current season
    /// </summary>
    public string? RiceVarietyName { get; set; }
    
    /// <summary>
    /// Optional: Plot boundary polygon in WKT format
    /// Example: POLYGON((105.123 10.456, 105.124 10.457, ...))
    /// If provided, plot will be created as Active status
    /// If empty, supervisor will be assigned to draw polygon
    /// </summary>
    public string? BoundaryWKT { get; set; }
    
    /// <summary>
    /// Optional: Plot center point in WKT format
    /// Example: POINT(105.123 10.456)
    /// If not provided, centroid of boundary will be used
    /// </summary>
    public string? CoordinateWKT { get; set; }
}

/// <summary>
/// Reference data for rice varieties - shown in second sheet of template
/// </summary>
public class RiceVarietyReference
{
    public string VarietyName { get; set; }
    public string SeasonType { get; set; }
    public string GrowthDuration { get; set; }
    public string Description { get; set; }
}

