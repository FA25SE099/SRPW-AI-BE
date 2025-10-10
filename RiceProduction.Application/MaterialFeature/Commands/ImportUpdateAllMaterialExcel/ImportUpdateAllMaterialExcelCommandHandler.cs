using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportUpdateAllMaterialExcel
{
    public class ImportUpdateAllMaterialExcelCommandHandler : IRequestHandler<ImportUpdateAllMaterialExcelCommand, Result<List<MaterialResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;
        public ImportUpdateAllMaterialExcelCommandHandler(IUnitOfWork unitOfWork, IGenericExcel genericExcel)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
        }
        public async Task<Result<List<MaterialResponse>>> Handle(ImportUpdateAllMaterialExcelCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var materialList = await _genericExcel.ExcelToListT<MaterialResponse>(request.ExcelFile);
                if (materialList == null || !materialList.Any())
                {
                    return Result<List<MaterialResponse>>.Failure("The uploaded Excel file is empty or invalid.");
                }
                //var result = Result<List<MaterialResponse>>.Success(materialList, "Convert excel to list success!");
                return Result<List<MaterialResponse>>.Success(materialList, "Convert excel to list success!");

            }
            catch (Exception ex)
            {
                return Result<List<MaterialResponse>>.Failure($"An error occurred while importing materials: {ex.Message}");
            }
        }
    }
}
