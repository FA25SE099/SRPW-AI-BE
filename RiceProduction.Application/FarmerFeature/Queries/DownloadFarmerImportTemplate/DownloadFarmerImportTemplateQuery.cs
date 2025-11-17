using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerImportTemplate;

/// <summary>
/// Query to download Excel template for farmer import
/// Includes sample data to guide cluster managers
/// </summary>
public class DownloadFarmerImportTemplateQuery : IRequest<Result<IActionResult>>
{
}

