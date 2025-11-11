using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.DownloadSample
{
    public class DownloadPlotSampleExcelQueryHandler : IRequestHandler<DownloadPlotSampleExcelQuery, Result<IActionResult>>
    {
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<DownloadPlotSampleExcelQueryHandler> _logger;

        public DownloadPlotSampleExcelQueryHandler(
            IGenericExcel genericExcel,
            ILogger<DownloadPlotSampleExcelQueryHandler> logger)
        {
            _genericExcel = genericExcel;
            _logger = logger;
        }

        public async Task<Result<IActionResult>> Handle(DownloadPlotSampleExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var sampleData = new List<PlotRequest>
                {
                    new PlotRequest
                    {
                        FarmerId = new Guid("12345678-1234-1234-1234-123456789012"),
                        GroupId = new Guid("87654321-4321-4321-4321-210987654321"),
                        SoThua = 1,
                        SoTo = 1,
                        Area = 1000.50m,
                        SoilType = "Đất phù sa",
                        Status = PlotStatus.Active,
                    },
                    new PlotRequest
                    {
                        FarmerId = new Guid("12345678-1234-1234-1234-123456789013"),
                        GroupId = new Guid("87654321-4321-4321-4321-210987654322"),
                        SoThua = 2,
                        SoTo = 1,
                        Area = 850.75m,
                        SoilType = "Đất cát",
                        Status = PlotStatus.Active,
                    },
                    new PlotRequest
                    {
                        FarmerId = new Guid("12345678-1234-1234-1234-123456789014"),
                        GroupId = null, // Optional field example
                        SoThua = 3,
                        SoTo = 2,
                        Area = 1200.00m,
                        SoilType = "Đất sét",
                        Status = PlotStatus.Active,
                    }
                };

                var fileName = "Mau_thua_ruong.xlsx";
                var result = await _genericExcel.DownloadGenericExcelFile(
                    sampleData,
                    DateTime.Now.ToString("yyyy-MM-dd"),
                    fileName);

                return Result<IActionResult>.Success(result, "Sample Plot Excel template downloaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating sample Plot Excel template");
                return Result<IActionResult>.Failure("Failed to create sample Plot Excel template");
            }
        }
    }
}
