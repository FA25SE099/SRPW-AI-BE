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
                var materialListInput = await _genericExcel.ExcelToListT<MaterialResponse>(request.ExcelFile);
                if (materialListInput == null || !materialListInput.Any())
                {
                    return Result<List<MaterialResponse>>.Failure("The uploaded Excel file is empty or invalid.");
                }
                foreach (var material in materialListInput)
                {
                    var materialInDB = _unitOfWork.Repository<Material>().FindAsync(m => material.MaterialId == m.Id);
                    if (materialInDB == null)
                    {
                        return Result<List<MaterialResponse>>.Failure($"The uploaded Excel file contain material name {material.Name} with ID changed. Please download file again to get the right ID!");
                    }
                }
                var materialRepo = _unitOfWork.Repository<Material>();
                var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();
                var materialList = new List<Material>();
                var materialPriceUpdateList = new List<MaterialPrice>();
                var materialPriceCreateList = new List<MaterialPrice>();
                foreach (var material in materialListInput)
                {
                    var materialInDB = await materialRepo.FindAsync(m => material.MaterialId == m.Id);
                    var materialPriceInDB = materialPriceRepo.ListAsync(p => p.MaterialId == material.MaterialId && material.IsActive && p.ValidFrom <= DateTime.UtcNow)
                        .Result.FirstOrDefault();
                    materialInDB.Name = material.Name;
                    materialInDB.Type = material.Type;
                    materialInDB.AmmountPerMaterial = material.AmmountPerMaterial;
                    materialInDB.Unit = material.Unit;
                    materialInDB.Description = material.Description;
                    materialInDB.Manufacturer = material.Manufacturer;
                    materialInDB.IsActive = material.IsActive;
                    materialPriceInDB.ValidTo = request.ImportDate;
                    var newMaterialPrice = new MaterialPrice
                    {
                        MaterialId = material.MaterialId,
                        PricePerMaterial = material.PricePerMaterial,
                        ValidFrom = request.ImportDate,
                        ValidTo = null
                    };
                    materialList.Add(materialInDB);
                    //materialPriceCreateList.Add(materialPriceInDB);
                    materialPriceCreateList.Add(newMaterialPrice);
                }
                // If all materials are valid, proceed to update
                materialRepo.UpdateRange(materialList);
                await materialPriceRepo.AddRangeAsync(materialPriceCreateList);
                //materialPriceRepo.UpdateRange(materialPriceUpdateList);
                await _unitOfWork.CompleteAsync();
                var result = Result<List<MaterialResponse>>.Success(materialListInput, "Convert excel to list success!");
                return Result<List<MaterialResponse>>.Success(materialListInput, "Convert excel to list success!");

            }
            catch (Exception ex)
            {
                return Result<List<MaterialResponse>>.Failure($"An error occurred while importing materials: {ex.Message}");
            }
        }
    }
}
