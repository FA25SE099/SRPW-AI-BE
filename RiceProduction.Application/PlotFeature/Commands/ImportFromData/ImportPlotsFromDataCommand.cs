using System;
using System.Collections.Generic;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.Common.Models.Response.PlotResponse;

namespace RiceProduction.Application.PlotFeature.Commands.ImportFromData;

/// <summary>
/// Command to import plots from data array (typically from UI after fixing errors in preview)
/// </summary>
public class ImportPlotsFromDataCommand : IRequest<Result<List<PlotResponse>>>
{
    /// <summary>
    /// Array of plot import rows to import
    /// </summary>
    public List<PlotImportRow> PlotRows { get; set; } = new();
    
    /// <summary>
    /// Cluster manager ID for assigning polygon tasks
    /// </summary>
    public Guid? ClusterManagerId { get; set; }
}
