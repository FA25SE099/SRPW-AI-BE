using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.PreviewPlotImport;

/// <summary>
/// Query to preview plot import from Excel file
/// Validates the file and returns what would be imported without actually saving to database
/// </summary>
public class PreviewPlotImportQuery : IRequest<Result<PlotImportPreviewDto>>
{
    /// <summary>
    /// Excel file to preview
    /// </summary>
    public required IFormFile ExcelFile { get; set; }
}
