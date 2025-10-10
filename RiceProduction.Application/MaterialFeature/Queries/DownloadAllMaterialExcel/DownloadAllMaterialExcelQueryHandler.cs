using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
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
    public class DownloadAllMaterialExcelQueryHandler : IRequestHandler<DownloadAllMaterialExcelQuery, Result<IActionResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;

        public DownloadAllMaterialExcelQueryHandler(IUnitOfWork unitOfWork, IGenericExcel genericExcel)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
        }

        public async Task<Result<IActionResult>> Handle(DownloadAllMaterialExcelQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var materialRepo = await _unitOfWork.Repository<Material>().ListAsync(
                    filter: m => m.IsActive,
                    orderBy: q => q.OrderBy(m => m.Name));
                var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();
                // Filter for active materials of the requested type
                Expression<Func<Material, bool>> filter = m => m.IsActive;

                var materialResponses = materialRepo
                    .Select(m => new MaterialResponse
                    {
                        MaterialId = m.Id,
                        Name = m.Name,
                        Type = m.Type,
                        AmmountPerMaterial = m.AmmountPerMaterial,
                        Showout = m.AmmountPerMaterial.ToString() + m.Unit,
                        PricePerMaterial = materialPriceRepo.ListAsync(p => p.MaterialId == m.Id && m.IsActive && 
                        (p.ValidFrom <= request.InputDate && (p.ValidTo >= request.InputDate || !p.ValidTo.HasValue))
                        ).Result.FirstOrDefault().PricePerMaterial,
                        Unit = m.Unit,
                        Description = m.Description,
                        Manufacturer = m.Manufacturer,
                        IsActive = m.IsActive
                    })
                    .ToList();
                var result = await _genericExcel.DownloadGenericExcelFile(materialResponses, request.InputDate.ToString(), "Bảng giá sản phẩm ngày " + request.InputDate);
                return Result<IActionResult>.Success(result,"File created successfully");

            }
            catch (Exception ex)
            {
                return Result<IActionResult>.Failure("File created successfully");
            }
        }

    }
}
