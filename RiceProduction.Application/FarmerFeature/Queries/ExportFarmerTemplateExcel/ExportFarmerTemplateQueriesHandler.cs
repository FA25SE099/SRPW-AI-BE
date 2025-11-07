using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialFeature.Queries.DownloadCreateSampleMaterialExcel;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Queries.ExportFarmerTemplateExcel
{
    public class ExportFarmerTemplateQueriesHandler : IRequestHandler<ExportFarmerTemplateQueries, Result<IActionResult>>
    {
        private readonly IGenericExcel _genericExcel;
        public ExportFarmerTemplateQueriesHandler(IGenericExcel genericExcel)
        {
            _genericExcel = genericExcel;
        }
        public async Task<Result<IActionResult>> Handle(ExportFarmerTemplateQueries request, CancellationToken cancellationToken)
        {
            try
            {
                var farmerList = new List<FarmerDTO>
                {
                    new FarmerDTO
                    {
                        FullName = "Nguyễn Văn A",
                        Address = "36/6 Thanh Hóa",
                        PhoneNumber = "09036363636",
                        IsActive = true,
                        FarmCode = "36TH",
                        PlotCount = 2
                    },
                    
                    new FarmerDTO
                    {
                        FullName = "Nguyễn Văn B",
                        Address = "18 Nam Định",
                        PhoneNumber = "0901818181818",
                        IsActive = true,
                        FarmCode = "18ND",
                        PlotCount = 4
                    }
                };
                var result = await _genericExcel.DownloadGenericExcelFile(farmerList, DateTime.UtcNow.ToString(), "Mẫu dữ liệu nông dân ngày" + DateTime.UtcNow);
                return Result<IActionResult>.Success(result, "File created successfully");
            }
            catch (Exception ex)
            {
                return Result<IActionResult>.Failure($"Error generating Excel file: {ex.Message}");
            }
        }
    }
}
