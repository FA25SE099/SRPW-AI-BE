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
                var materialRepo = _unitOfWork.Repository<Material>();
                var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();
                foreach (var material in materialListInput)
                {
                    var materialInDB = await materialRepo.FindAsync(m => material.MaterialId == m.Id);
                    if (materialInDB == null)
                    {
                        return Result<List<MaterialResponse>>.Failure($"The uploaded Excel file contain material name {material.Name} with ID changed. Please download file again to get the right ID!");
                    }
                }
                var materialList = new List<Material>();
                var materialPriceUpdateList = new List<MaterialPrice>();
                var materialPriceCreateList = new List<MaterialPrice>();
                foreach (var material in materialListInput)
                {
                    // Get material from database
                    var materialInDB = await materialRepo.FindAsync(m => material.MaterialId == m.Id);
                    if (materialInDB == null)
                    {
                        return Result<List<MaterialResponse>>.Failure(
                            $"Material with ID {material.MaterialId} not found.");
                    }

                    var currentDate = DateTime.UtcNow;
                    // FIX 2: Fixed filter logic - get current active price (where ValidTo is null)
                    var materialPriceInDB = (await materialPriceRepo.ListAsync(
                        p => p.MaterialId == material.MaterialId &&
                             (p.ValidFrom.CompareTo(currentDate) <= 0) && 
                             (!p.ValidTo.HasValue || (p.ValidTo.Value.Date.CompareTo(currentDate) >= 0))))
                             .OrderByDescending(p => p.ValidFrom)
                        .FirstOrDefault();

                    // Update material properties
                    materialInDB.Name = material.Name;
                    materialInDB.Type = material.Type;
                    materialInDB.AmmountPerMaterial = material.AmmountPerMaterial;
                    materialInDB.Unit = material.Unit;
                    materialInDB.Description = material.Description;
                    materialInDB.Manufacturer = material.Manufacturer;
                    materialInDB.IsActive = material.IsActive;

                    // FIX 3: Close old price if it exists and price changed
                    if (materialPriceInDB != null)
                    {
                        // Only update if price has changed
                        if (materialPriceInDB.PricePerMaterial != material.PricePerMaterial)
                        {
                            materialPriceInDB.ValidTo = request.ImportDate;
                            materialPriceUpdateList.Add(materialPriceInDB);

                            // Create new price record
                            var newMaterialPrice = new MaterialPrice
                            {
                                MaterialId = material.MaterialId,
                                PricePerMaterial = material.PricePerMaterial,
                                ValidFrom = request.ImportDate,
                                ValidTo = null
                            };
                            materialPriceCreateList.Add(newMaterialPrice);
                        }
                    }
                    else
                    {
                        // FIX 4: If no existing price, create one
                        var newMaterialPrice = new MaterialPrice
                        {
                            MaterialId = material.MaterialId,
                            PricePerMaterial = material.PricePerMaterial,
                            ValidFrom = request.ImportDate,
                            ValidTo = null
                        };
                        materialPriceCreateList.Add(newMaterialPrice);
                    }

                    materialList.Add(materialInDB);
                }

                // FIX 5: Update old prices before adding new ones
                if (materialPriceUpdateList.Any())
                {
                    materialPriceRepo.UpdateRange(materialPriceUpdateList);
                }

                // Update materials
                materialRepo.UpdateRange(materialList);

                // Add new price records
                if (materialPriceCreateList.Any())
                {
                    await materialPriceRepo.AddRangeAsync(materialPriceCreateList);
                }

                // Save all changes
                await _unitOfWork.CompleteAsync();

                return Result<List<MaterialResponse>>.Success(
                    materialListInput,
                    $"Successfully updated {materialList.Count} materials and {materialPriceCreateList.Count} price records!");
            }
            catch (Exception ex)
            {
                return Result<List<MaterialResponse>>.Failure($"An error occurred while importing materials: {ex.Message}");
            }
        }
    }
}
