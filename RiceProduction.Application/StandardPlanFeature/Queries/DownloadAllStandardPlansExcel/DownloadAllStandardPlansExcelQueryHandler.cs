using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Application.StandardPlanFeature.Queries.DownloadAllStandardPlansExcel
{
    public class DownloadAllStandardPlansExcelQueryHandler : IRequestHandler<DownloadAllStandardPlansExcelQuery, Result<IActionResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<DownloadAllStandardPlansExcelQueryHandler> _logger;

        public DownloadAllStandardPlansExcelQueryHandler(
            IUnitOfWork unitOfWork, 
            IGenericExcel genericExcel,
            ILogger<DownloadAllStandardPlansExcelQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
            _logger = logger;
        }

        public async Task<Result<IActionResult>> Handle(DownloadAllStandardPlansExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting standard plans Excel export for date: {InputDate}", request.InputDate);

                Expression<Func<StandardPlan, bool>> filter = sp =>
                    (request.CategoryId == null || sp.CategoryId == request.CategoryId.Value) &&
                    (request.IsActive == null || sp.IsActive == request.IsActive.Value);

                var standardPlans = await _unitOfWork.Repository<StandardPlan>().ListAsync(
                    filter: filter,
                    orderBy: q => q.OrderBy(sp => sp.Category.CategoryName).ThenBy(sp => sp.PlanName),
                    includeProperties: q => q
                        .Include(sp => sp.Category)
                        .Include(sp => sp.StandardPlanStages)
                            .ThenInclude(sps => sps.StandardPlanTasks));

                if (standardPlans == null || !standardPlans.Any())
                {
                    _logger.LogWarning("No standard plans found for export");
                    return Result<IActionResult>.Failure("No standard plans found to export");
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

                var sheetName = $"StandardPlans_{request.InputDate:yyyyMMdd}";
                var fileName = $"Ke_hoach_chuan_{request.InputDate:yyyyMMdd}.xlsx";
                
                var result = await _genericExcel.DownloadGenericExcelFile(
                    standardPlanDtos, 
                    request.InputDate.ToString("yyyy-MM-dd"), 
                    fileName);

                _logger.LogInformation("Successfully exported {Count} standard plans to Excel", standardPlanDtos.Count);
                return Result<IActionResult>.Success(result, "Standard plans exported successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while exporting standard plans to Excel");
                return Result<IActionResult>.Failure("Failed to export standard plans to Excel");
            }
        }
    }
}

