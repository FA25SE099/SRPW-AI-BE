using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerImportTemplate;

/// <summary>
/// Query to download Excel template for farmer import
/// Includes sample data to guide cluster managers
/// </summary>
public class DownloadFarmerImportTemplateQuery : IRequest<Result<FileExportResult>>
{
}

