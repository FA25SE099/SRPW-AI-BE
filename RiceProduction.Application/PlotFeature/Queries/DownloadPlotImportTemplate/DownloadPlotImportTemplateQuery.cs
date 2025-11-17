using System;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.DownloadPlotImportTemplate;

/// <summary>
/// Query to download personalized Excel template for plot import
/// Generates one row per plot based on each farmer's NumberOfPlots
/// Includes rice variety reference sheet
/// </summary>
public class DownloadPlotImportTemplateQuery : IRequest<Result<IActionResult>>
{
    /// <summary>
    /// Cluster manager ID to filter farmers by cluster
    /// If null, includes all farmers
    /// </summary>
    public Guid? ClusterManagerId { get; set; }
}

