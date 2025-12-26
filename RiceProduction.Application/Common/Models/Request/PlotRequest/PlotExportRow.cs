using System;

namespace RiceProduction.Application.Common.Models.Request.PlotRequest;

/// <summary>
/// DTO for exporting plot data with polygon information
/// </summary>
public class PlotExportRow
{
    /// <summary>
    /// Unique farm code to identify the farmer
    /// </summary>
    public string FarmCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Farmer's full name
    /// </summary>
    public string FarmerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Farmer's phone number
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Land parcel number (Số thửa)
    /// </summary>
    public int? SoThua { get; set; }
    
    /// <summary>
    /// Land map sheet number (Số tờ)
    /// </summary>
    public int? SoTo { get; set; }
    
    /// <summary>
    /// Plot area in hectares
    /// </summary>
    public decimal? Area { get; set; }
    
    /// <summary>
    /// Soil type (e.g., Clay, Sandy, Loam)
    /// </summary>
    public string? SoilType { get; set; }
    
    /// <summary>
    /// Plot status
    /// </summary>
    public string? Status { get; set; }
    
    /// <summary>
    /// Plot boundary polygon in WKT format
    /// Example: POLYGON((105.123 10.456, 105.124 10.457, ...))
    /// </summary>
    public string? BoundaryWKT { get; set; }
    
    /// <summary>
    /// Plot center point in WKT format
    /// Example: POINT(105.123 10.456)
    /// </summary>
    public string? CoordinateWKT { get; set; }
    
    /// <summary>
    /// Current rice variety being cultivated (if any)
    /// </summary>
    public string? CurrentRiceVariety { get; set; }
    
    /// <summary>
    /// Group name (if assigned to a group)
    /// </summary>
    public string? GroupName { get; set; }
}

