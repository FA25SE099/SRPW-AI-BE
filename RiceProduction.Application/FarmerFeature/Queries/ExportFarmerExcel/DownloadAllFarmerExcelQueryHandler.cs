using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Mappings;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerExcel
{
    public class DownloadAllFarmerExcelQueryHandler : IRequestHandler<DownloadAllFarmerExcelQuery, Result<FileExportResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _downloadGenericExcel;

        public DownloadAllFarmerExcelQueryHandler(IUnitOfWork unitOfWork, IGenericExcel downloadGenericExcel)
        {
            _unitOfWork = unitOfWork;
            _downloadGenericExcel = downloadGenericExcel;
        }

        public async Task<Result<FileExportResult>> Handle(DownloadAllFarmerExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var farmers = await _unitOfWork.FarmerRepository.GetAllFarmerAsync(cancellationToken);

                if (farmers == null || !farmers.Any())
                {
                    return Result<FileExportResult>.Failure("No farmers found to export");
                }
                var farmerDTOs = farmers.Where(f => f != null).Select(f => f!.ToDto()).ToList();
                var sheetName = $"Farmers_{request.InputDate:yyyyMMdd}";
                var fileName = $"{sheetName}.xlsx";
                var result = await _downloadGenericExcel.DownloadGenericExcelFile(
                farmerDTOs,
                sheetName,
                fileName);
                return Result<FileExportResult>.Success(result, "Farmer download successfully");
            }
            catch (Exception ex)
            {
                return Result<FileExportResult>.Failure("File created fail");
            }
        }
    }
}
