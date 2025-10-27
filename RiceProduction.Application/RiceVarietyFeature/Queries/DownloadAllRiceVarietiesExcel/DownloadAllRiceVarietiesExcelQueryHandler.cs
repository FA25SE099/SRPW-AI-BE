using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.DownloadAllRiceVarietiesExcel
{
    public class DownloadAllRiceVarietiesExcelQueryHandler : IRequestHandler<DownloadAllRiceVarietiesExcelQuery, Result<IActionResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<DownloadAllRiceVarietiesExcelQueryHandler> _logger;

        public DownloadAllRiceVarietiesExcelQueryHandler(
            IUnitOfWork unitOfWork, 
            IGenericExcel genericExcel,
            ILogger<DownloadAllRiceVarietiesExcelQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
            _logger = logger;
        }

        public async Task<Result<IActionResult>> Handle(DownloadAllRiceVarietiesExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting rice varieties Excel export for date: {InputDate}", request.InputDate);

                Expression<Func<RiceVariety, bool>> filter = v =>
                    (request.CategoryId == null || v.CategoryId == request.CategoryId.Value) &&
                    (request.IsActive == null || v.IsActive == request.IsActive.Value);

                var riceVarieties = await _unitOfWork.Repository<RiceVariety>().ListAsync(
                    filter: filter,
                    orderBy: q => q.OrderBy(v => v.Category.CategoryName).ThenBy(v => v.VarietyName),
                    includeProperties: q => q.Include(v => v.Category));

                if (riceVarieties == null || !riceVarieties.Any())
                {
                    _logger.LogWarning("No rice varieties found for export");
                    return Result<IActionResult>.Failure("No rice varieties found to export");
                }

                var riceVarietyResponses = riceVarieties.Select(v => new RiceVarietyResponse
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
                }).ToList();

                var sheetName = $"RiceVarieties_{request.InputDate:yyyyMMdd}";
                var fileName = $"Danh_sach_giong_lua_{request.InputDate:yyyyMMdd}.xlsx";
                
                var result = await _genericExcel.DownloadGenericExcelFile(
                    riceVarietyResponses, 
                    request.InputDate.ToString("yyyy-MM-dd"), 
                    fileName);

                _logger.LogInformation("Successfully exported {Count} rice varieties to Excel", riceVarietyResponses.Count);
                return Result<IActionResult>.Success(result, "Rice varieties exported successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while exporting rice varieties to Excel");
                return Result<IActionResult>.Failure("Failed to export rice varieties to Excel");
            }
        }
    }
}

