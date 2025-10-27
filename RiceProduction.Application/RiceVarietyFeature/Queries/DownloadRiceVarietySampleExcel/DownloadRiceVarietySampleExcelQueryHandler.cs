using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.DownloadRiceVarietySampleExcel
{
    public class DownloadRiceVarietySampleExcelQueryHandler : IRequestHandler<DownloadRiceVarietySampleExcelQuery, Result<IActionResult>>
    {
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<DownloadRiceVarietySampleExcelQueryHandler> _logger;

        public DownloadRiceVarietySampleExcelQueryHandler(
            IGenericExcel genericExcel,
            ILogger<DownloadRiceVarietySampleExcelQueryHandler> logger)
        {
            _genericExcel = genericExcel;
            _logger = logger;
        }

        public async Task<Result<IActionResult>> Handle(DownloadRiceVarietySampleExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var sampleData = new List<RiceVarietyResponse>
                {
                    new RiceVarietyResponse
                    {
                        Id = Guid.NewGuid(),
                        VarietyName = "ST25",
                        CategoryId = Guid.NewGuid(),
                        CategoryName = "Giống ngắn ngày",
                        BaseGrowthDurationDays = 95,
                        BaseYieldPerHectare = 6.5m,
                        Description = "Giống lúa chất lượng cao, chống chịu hạn tốt",
                        Characteristics = "Thân cứng, bông to, hạt dài",
                        IsActive = true
                    },
                    new RiceVarietyResponse
                    {
                        Id = Guid.NewGuid(),
                        VarietyName = "OM5451",
                        CategoryId = Guid.NewGuid(),
                        CategoryName = "Giống dài ngày",
                        BaseGrowthDurationDays = 120,
                        BaseYieldPerHectare = 7.2m,
                        Description = "Giống lúa năng suất cao, thích hợp vùng đất phù sa",
                        Characteristics = "Thân to, lá rộng, chống chịu sâu bệnh tốt",
                        IsActive = true
                    }
                };

                var fileName = "Mau_nhap_lieu_giong_lua.xlsx";
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

