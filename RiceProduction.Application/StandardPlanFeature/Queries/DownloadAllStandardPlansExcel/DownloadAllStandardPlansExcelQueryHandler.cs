using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;

namespace RiceProduction.Application.StandardPlanFeature.Queries.DownloadAllStandardPlansExcel
{
    public class DownloadAllStandardPlansExcelQueryHandler : IRequestHandler<DownloadAllStandardPlansExcelQuery, Result<FileExportResult>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<DownloadAllStandardPlansExcelQueryHandler> _logger;

        public DownloadAllStandardPlansExcelQueryHandler(
            IApplicationDbContext context,
            IGenericExcel genericExcel,
            ILogger<DownloadAllStandardPlansExcelQueryHandler> logger)
        {
            _context = context;
            _genericExcel = genericExcel;
            _logger = logger;
        }

        public async Task<Result<FileExportResult>> Handle(DownloadAllStandardPlansExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting standard plans Excel export for date: {InputDate}", request.InputDate);

                var standardPlans = await _context.StandardPlans
                    .AsNoTracking()
                    .Where(sp =>
                        (request.CategoryId == null || sp.CategoryId == request.CategoryId.Value) &&
                        (request.IsActive == null || sp.IsActive == request.IsActive.Value))
                    .Include(sp => sp.Category)
                    .Include(sp => sp.StandardPlanStages)
                        .ThenInclude(sps => sps.StandardPlanTasks)
                    .AsSplitQuery()
                    .OrderBy(sp => sp.Category.CategoryName)
                    .ThenBy(sp => sp.PlanName)
                    .ToListAsync(cancellationToken);

                if (standardPlans.Count == 0)
                {
                    _logger.LogWarning("No standard plans found for export");
                    return Result<FileExportResult>.Failure("No standard plans found to export");
                }

                var standardPlanDtos = standardPlans.Select(sp => new StandardPlanDto
                {
                    Id = sp.Id,
                    Name = sp.PlanName,
                    Description = sp.Description,
                    CategoryId = sp.CategoryId,
                    CategoryName = sp.Category.CategoryName,
                    TotalDuration = sp.TotalDurationDays,
                    IsActive = sp.IsActive,
                    TotalTasks = sp.StandardPlanStages?
                        .SelectMany(sps => sps.StandardPlanTasks)
                        .Count() ?? 0,
                    TotalStages = sp.StandardPlanStages?.Count ?? 0,
                    CreatedAt = sp.CreatedAt,
                    CreatedBy = sp.CreatedBy,
                    LastModified = sp.LastModified,
                    LastModifiedBy = sp.LastModifiedBy
                }).ToList();

                var fileName = $"Ke_hoach_chuan_{request.InputDate:yyyyMMdd}.xlsx";
                
                var result = await _genericExcel.DownloadGenericExcelFile(
                    standardPlanDtos, 
                    request.InputDate.ToString("yyyy-MM-dd"), 
                    fileName);

                _logger.LogInformation("Successfully exported {Count} standard plans to Excel", standardPlanDtos.Count);
                return Result<FileExportResult>.Success(result, "Standard plans exported successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while exporting standard plans to Excel");
                return Result<FileExportResult>.Failure("Failed to export standard plans to Excel");
            }
        }
    }
}

