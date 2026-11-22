using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;

namespace RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerExcel
{
    public class DownloadAllFarmerExcelQueryHandler : IRequestHandler<DownloadAllFarmerExcelQuery, Result<FileExportResult>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IGenericExcel _downloadGenericExcel;

        public DownloadAllFarmerExcelQueryHandler(IApplicationDbContext context, IGenericExcel downloadGenericExcel)
        {
            _context = context;
            _downloadGenericExcel = downloadGenericExcel;
        }

        public async Task<Result<FileExportResult>> Handle(DownloadAllFarmerExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var farmerDTOs = await _context.Farmers
                    .AsNoTracking()
                    .Where(f => f.IsActive)
                    .OrderBy(f => f.FullName)
                    .Select(f => new FarmerDTO
                    {
                        FarmerId = f.Id,
                        FullName = f.FullName,
                        Address = f.Address,
                        PhoneNumber = f.PhoneNumber,
                        IsActive = f.IsActive,
                        IsVerified = f.IsVerified,
                        LastActivityAt = f.LastActivityAt,
                        FarmCode = f.FarmCode,
                        PlotCount = f.OwnedPlots.Count
                    })
                    .ToListAsync(cancellationToken);

                if (farmerDTOs.Count == 0)
                {
                    return Result<FileExportResult>.Failure("No farmers found to export");
                }

                var sheetName = $"Farmers_{request.InputDate:yyyyMMdd}";
                var fileName = $"{sheetName}.xlsx";
                var result = await _downloadGenericExcel.DownloadGenericExcelFile(
                    farmerDTOs,
                    sheetName,
                    fileName);
                return Result<FileExportResult>.Success(result, "Farmer download successfully");
            }
            catch (Exception)
            {
                return Result<FileExportResult>.Failure("File created fail");
            }
        }
    }
}
