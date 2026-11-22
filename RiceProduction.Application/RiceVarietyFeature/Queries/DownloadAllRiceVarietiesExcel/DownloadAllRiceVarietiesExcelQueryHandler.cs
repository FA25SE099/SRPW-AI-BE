using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.DownloadAllRiceVarietiesExcel
{
    public class DownloadAllRiceVarietiesExcelQueryHandler : IRequestHandler<DownloadAllRiceVarietiesExcelQuery, Result<FileExportResult>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<DownloadAllRiceVarietiesExcelQueryHandler> _logger;

        public DownloadAllRiceVarietiesExcelQueryHandler(
            IApplicationDbContext context,
            IGenericExcel genericExcel,
            ILogger<DownloadAllRiceVarietiesExcelQueryHandler> logger)
        {
            _context = context;
            _genericExcel = genericExcel;
            _logger = logger;
        }

        public async Task<Result<FileExportResult>> Handle(DownloadAllRiceVarietiesExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting rice varieties Excel export for date: {InputDate}", request.InputDate);

                var query = _context.RiceVarieties.AsNoTracking();

                if (request.CategoryId.HasValue)
                {
                    query = query.Where(v => v.CategoryId == request.CategoryId.Value);
                }

                if (request.IsActive.HasValue)
                {
                    query = query.Where(v => v.IsActive == request.IsActive.Value);
                }

                var riceVarietyResponses = await query
                    .OrderBy(v => v.Category.CategoryName)
                    .ThenBy(v => v.VarietyName)
                    .Select(v => new RiceVarietyResponse
                    {
                        Id = v.Id,
                        VarietyName = v.VarietyName,
                        CategoryId = v.CategoryId,
                        CategoryName = v.Category.CategoryName,
                        BaseGrowthDurationDays = v.BaseGrowthDurationDays,
                        BaseYieldPerHectare = v.BaseYieldPerHectare,
                        Description = v.Description,
                        Characteristics = v.Characteristics,
                        IsActive = v.IsActive
                    })
                    .ToListAsync(cancellationToken);

                if (riceVarietyResponses.Count == 0)
                {
                    _logger.LogWarning("No rice varieties found for export");
                    return Result<FileExportResult>.Failure("No rice varieties found to export");
                }

                var fileName = $"Danh_sach_giong_lua_{request.InputDate:yyyyMMdd}.xlsx";

                var result = await _genericExcel.DownloadGenericExcelFile(
                    riceVarietyResponses,
                    request.InputDate.ToString("yyyy-MM-dd"),
                    fileName);

                _logger.LogInformation("Successfully exported {Count} rice varieties to Excel", riceVarietyResponses.Count);
                return Result<FileExportResult>.Success(result, "Rice varieties exported successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while exporting rice varieties to Excel");
                return Result<FileExportResult>.Failure("Failed to export rice varieties to Excel");
            }
        }
    }
}

