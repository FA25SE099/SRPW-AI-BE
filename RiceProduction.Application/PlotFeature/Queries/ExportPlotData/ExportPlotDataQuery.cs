using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.ExportPlotData;

public class ExportPlotDataQuery : IRequest<Result<IActionResult>>
{
    /// <summary>
    /// Optional: Filter by cluster manager ID
    /// If provided, only exports plots from farmers in that cluster
    /// </summary>
    public Guid? ClusterManagerId { get; set; }
    
    /// <summary>
    /// Optional: Filter by farmer ID
    /// If provided, only exports plots for that specific farmer
    /// </summary>
    public Guid? FarmerId { get; set; }
    
    /// <summary>
    /// Optional: Filter by group ID
    /// If provided, only exports plots in that group
    /// </summary>
    public Guid? GroupId { get; set; }
    
    /// <summary>
    /// Include only plots with polygons (default: false - includes all)
    /// </summary>
    public bool OnlyWithPolygons { get; set; } = false;
    
    /// <summary>
    /// Include only plots without polygons (default: false - includes all)
    /// </summary>
    public bool OnlyWithoutPolygons { get; set; } = false;
}

