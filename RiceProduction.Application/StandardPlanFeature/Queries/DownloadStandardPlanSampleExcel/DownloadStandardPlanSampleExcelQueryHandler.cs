using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;

namespace RiceProduction.Application.StandardPlanFeature.Queries.DownloadStandardPlanSampleExcel
{
    public class DownloadStandardPlanSampleExcelQueryHandler : IRequestHandler<DownloadStandardPlanSampleExcelQuery, Result<IActionResult>>
    {
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<DownloadStandardPlanSampleExcelQueryHandler> _logger;

        public DownloadStandardPlanSampleExcelQueryHandler(
            IGenericExcel genericExcel,
            ILogger<DownloadStandardPlanSampleExcelQueryHandler> logger)
        {
            _genericExcel = genericExcel;
            _logger = logger;
        }

        public async Task<Result<IActionResult>> Handle(DownloadStandardPlanSampleExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var sampleData = new List<StandardPlanDto>
                {
                    new StandardPlanDto
                    {
                        Id = Guid.NewGuid(),
                        Name = "Kế hoạch canh tác giống ngắn ngày",
                        Description = "Kế hoạch canh tác tiêu chuẩn cho giống lúa ngắn ngày (dưới 100 ngày)",
                        CategoryId = Guid.NewGuid(),
                        CategoryName = "Giống ngắn ngày",
                        TotalDuration = 95,
                        IsActive = true,
                        TotalStages = 5,
                        TotalTasks = 15,
                        CreatedAt = DateTimeOffset.Now,
                        CreatedBy = Guid.NewGuid(),
                        LastModified = DateTimeOffset.Now,
                        LastModifiedBy = Guid.NewGuid()
                    },
                    new StandardPlanDto
                    {
                        Id = Guid.NewGuid(),
                        Name = "Kế hoạch canh tác giống dài ngày",
                        Description = "Kế hoạch canh tác tiêu chuẩn cho giống lúa dài ngày (trên 100 ngày)",
                        CategoryId = Guid.NewGuid(),
                        CategoryName = "Giống dài ngày",
                        TotalDuration = 120,
                        IsActive = true,
                        TotalStages = 6,
                        TotalTasks = 18,
                        CreatedAt = DateTimeOffset.Now,
                        CreatedBy = Guid.NewGuid(),
                        LastModified = DateTimeOffset.Now,
                        LastModifiedBy = Guid.NewGuid()
                    }
                };

                var fileName = "Mau_ke_hoach_chuan.xlsx";
                var result = await _genericExcel.DownloadGenericExcelFile(
                    sampleData, 
                    DateTime.Now.ToString("yyyy-MM-dd"), 
                    fileName);

                return Result<IActionResult>.Success(result, "Sample Excel template downloaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating sample Excel template");
                return Result<IActionResult>.Failure("Failed to create sample Excel template");
            }
        }
    }
}

