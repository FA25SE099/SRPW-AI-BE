using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.DownloadCreateSampleMaterialExcel
{
    public class DownloadCreateSampleMaterialExcelQueryHandler : IRequestHandler<DownloadCreateSampleMaterialExcelQuery, Result<IActionResult>>
    {
        private readonly IGenericExcel _genericExcel;
        public DownloadCreateSampleMaterialExcelQueryHandler(IGenericExcel genericExcel)
        {
            _genericExcel = genericExcel;
        }
        public async Task<Result<IActionResult>> Handle(DownloadCreateSampleMaterialExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var materialCreateResponses = new List<MaterialCreateRequest>
                {
                    new MaterialCreateRequest
                    {
                        Name = "Thuốc trừ sâu A 50ml",
                        Type = MaterialType.Pesticide,
                        AmmountPerMaterial = 50,
                        PricePerMaterial = 20000,
                        Unit = "ml",
                        Description = "Phun thuốc trước sạ, 100g/25 lít nước",
                        Manufacturer = "Công ty ABC",
                        IsActive = true
                    },
                    new MaterialCreateRequest
                    {
                        Name = "Phân Bón B",
                        Type = MaterialType.Fertilizer,
                        AmmountPerMaterial = 100,
                        PricePerMaterial = 150000,
                        Unit = "kg",
                        Description = "Bón thúc trước sạ, 100g/25 lít nước",
                        Manufacturer = "Công ty XYZ",
                        IsActive = true
                    }
                };
                var result = await _genericExcel.DownloadGenericExcelFile(materialCreateResponses, DateTime.UtcNow.ToString(), "Mẫu tạo sản phẩm ngày" + DateTime.UtcNow);
                return Result<IActionResult>.Success(result, "File created successfully");
            }
            catch (Exception ex)
            {
                return Result<IActionResult>.Failure($"Error generating Excel file: {ex.Message}");
            }
        }
    }
}
