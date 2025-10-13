using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.MaterialFeature.Commands.ImportUpdateAllMaterialExcel;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportCreateAllMaterialExcel
{
    public class ImportCreateAllMaterialExcelCommandHandler : IRequestHandler<ImportCreateAllMaterialExcelCommand, Result<List<MaterialResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;

        public ImportCreateAllMaterialExcelCommandHandler(IUnitOfWork unitOfWork, IGenericExcel genericExcel)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
        }

        public async Task<Result<List<MaterialResponse>>> Handle(ImportCreateAllMaterialExcelCommand request, CancellationToken cancellationToken)
        {
            try
            {
                //Change excel file back to list
                var materialListCreateInput = await _genericExcel.ExcelToListT<MaterialCreateRequest>(request.ExcelFile);
                if (materialListCreateInput == null || !materialListCreateInput.Any())
                {
                    return Result<List<MaterialResponse>>.Failure("The uploaded Excel file is empty or invalid.");
                }
                var materialRepo = _unitOfWork.Repository<Material>();
                var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();
                //Check for duplicate in the input list (check if exist already in the db with all fields)
                foreach (var material in materialListCreateInput)
                {
                    var duplicateCount = await materialRepo.FindAsync(m => m.Name == material.Name
                    && m.Type == material.Type && m.AmmountPerMaterial == material.AmmountPerMaterial
                    && m.Unit == material.Unit && m.Description == material.Description
                    && m.Manufacturer == material.Manufacturer);
                    if (duplicateCount != null)
                    {
                        return Result<List<MaterialResponse>>.Failure($"The uploaded Excel file contain duplicate material in the system, material name {material.Name}. Please check again!");
                    }
                }
                //list to hold material will be created
                var materialList = new List<Material>();
                //list to hold result to response to api
                var materialCreateSuccessList = new List<MaterialResponse>();
                var materialPriceCreateList = new List<MaterialPrice>();
                foreach (var material in materialListCreateInput)
                {
                    var id = await materialRepo.GenerateNewGuid(Guid.NewGuid());
                    var currentDate = DateTime.UtcNow;
                    var newMaterial = new Material
                    {
                        Id = id,
                        Name = material.Name,
                        Type = material.Type,
                        AmmountPerMaterial = material.AmmountPerMaterial,
                        Unit = material.Unit,
                        Description = material.Description,
                        Manufacturer = material.Manufacturer,
                        IsActive = material.IsActive,
                        MaterialPrices = new List<MaterialPrice>()
                        {
                            new MaterialPrice
                            {
                                MaterialId = id,
                                PricePerMaterial = material.PricePerMaterial,
                                ValidFrom = request.ImportDate,
                                ValidTo = null
                            }
                        }
                    };
                    //var newMaterialPrice = new MaterialPrice
                    //{
                    //    MaterialId = id,
                    //    PricePerMaterial = material.PricePerMaterial,
                    //    ValidFrom = request.ImportDate,
                    //    ValidTo = null
                    //};
                    //materialPriceCreateList.Add(newMaterialPrice);

                    materialList.Add(newMaterial);
                }

                // Add new material records
                if (materialList.Any())
                {
                    await materialRepo.AddRangeAsync(materialList);
                }

                //// Add new price records
                //if (materialPriceCreateList.Any())
                //{
                //    await materialPriceRepo.AddRangeAsync(materialPriceCreateList);
                //}

                // Save all changes
                var result = await _unitOfWork.CompleteAsync();
                if (result <= 0)
                {
                    return Result<List<MaterialResponse>>.Failure("Failed to import materials.");
                }
                foreach (var material in materialList)
                {
                    var materialResponse = new MaterialResponse
                    {
                        MaterialId = material.Id,
                        Name = material.Name,
                        Type = material.Type,
                        AmmountPerMaterial = material.AmmountPerMaterial,
                        Showout = material.AmmountPerMaterial.ToString() + material.Unit,
                        PricePerMaterial = materialPriceRepo
                        .ListAsync(p => p.MaterialId == material.Id).Result
                        .OrderByDescending(p => p.ValidFrom).FirstOrDefault()?.PricePerMaterial ?? 0,
                        Unit = material.Unit,
                        Description = material.Description,
                        Manufacturer = material.Manufacturer,
                        IsActive = material.IsActive
                    };
                    materialCreateSuccessList.Add(materialResponse);
                }
                return Result<List<MaterialResponse>>.Success(
                    materialCreateSuccessList,
                    $"Successfully created {materialCreateSuccessList.Count} materials!");
            }
            catch (Exception ex)
            {
                return Result<List<MaterialResponse>>.Failure($"An error occurred while importing materials: {ex.Message}");
            }
        }
    }
}
