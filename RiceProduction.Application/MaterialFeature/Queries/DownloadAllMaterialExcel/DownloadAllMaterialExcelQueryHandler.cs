using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel
{
    public class DownloadAllMaterialExcelQueryHandler : IRequestHandler<DownloadAllMaterialExcelQuery, Result<FileExportResult>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IGenericExcel _genericExcel;

        public DownloadAllMaterialExcelQueryHandler(IApplicationDbContext context, IGenericExcel genericExcel)
        {
            _context = context;
            _genericExcel = genericExcel;
        }

        public async Task<Result<FileExportResult>> Handle(DownloadAllMaterialExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var currentDate = DateTime.UtcNow;

                var materials = await _context.Materials
                    .AsNoTracking()
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.Name)
                    .ToListAsync(cancellationToken);

                var materialIds = materials.Select(m => m.Id).ToList();
                var prices = await _context.MaterialPrices
                    .AsNoTracking()
                    .Where(p => materialIds.Contains(p.MaterialId)
                        && p.ValidFrom <= currentDate
                        && (!p.ValidTo.HasValue || p.ValidTo.Value >= currentDate.Date))
                    .Select(p => new { p.MaterialId, p.ValidFrom, p.PricePerMaterial })
                    .ToListAsync(cancellationToken);

                var latestPriceByMaterial = prices
                    .GroupBy(p => p.MaterialId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.ValidFrom).First().PricePerMaterial);

                var materialResponses = materials
                    .Select(m => new MaterialResponseInVietnamese()
                    {
                        MaterialId = m.Id,
                        Name = m.Name,
                        Type = m.Type,
                        AmmountPerMaterial = m.AmmountPerMaterial,
                        Showout = m.AmmountPerMaterial.ToString() + m.Unit,
                        PricePerMaterial = latestPriceByMaterial.TryGetValue(m.Id, out var price) ? price : 0,
                        Unit = m.Unit,
                        Description = m.Description,
                        Manufacturer = m.Manufacturer,
                        IsActive = m.IsActive,
                        IsPartition = m.IsPartition
                    }).OrderByDescending(p => p.Type)
                    .ToList();
                var result = await _genericExcel.DownloadGenericExcelFile(materialResponses, request.InputDate.ToString(), "Bảng giá sản phẩm ngày " + request.InputDate);
                return Result<FileExportResult>.Success(result,"File created successfully");

            }
            catch (Exception)
            {
                return Result<FileExportResult>.Failure("File created fail");
            }
        }

    }
}
