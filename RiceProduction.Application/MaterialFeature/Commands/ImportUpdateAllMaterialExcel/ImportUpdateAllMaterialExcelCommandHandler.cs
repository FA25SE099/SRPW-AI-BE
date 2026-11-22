using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportUpdateAllMaterialExcel
{
    public class ImportUpdateAllMaterialExcelCommandHandler : IRequestHandler<ImportUpdateAllMaterialExcelCommand, Result<List<MaterialResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IGenericExcel _genericExcel;

        public ImportUpdateAllMaterialExcelCommandHandler(IApplicationDbContext context, IGenericExcel genericExcel)
        {
            _context = context;
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

                var materialsInDbById = new Dictionary<Guid, Material>();
                foreach (var material in materialListInput)
                {
                    var materialInDB = await _context.Materials
                        .FirstOrDefaultAsync(m => m.Id == material.MaterialId, cancellationToken);
                    if (materialInDB == null)
                    {
                        return Result<List<MaterialResponse>>.Failure($"The uploaded Excel file contain material name {material.Name} with ID changed. Please download file again to get the right ID!");
                    }
                    materialsInDbById[material.MaterialId] = materialInDB;
                }

                var materialList = new List<Material>();
                var materialPriceUpdateList = new List<MaterialPrice>();
                var materialPriceCreateList = new List<MaterialPrice>();
                var currentDate = DateTime.UtcNow;

                foreach (var material in materialListInput)
                {
                    if (!materialsInDbById.TryGetValue(material.MaterialId, out var materialInDB))
                    {
                        return Result<List<MaterialResponse>>.Failure(
                            $"Material with ID {material.MaterialId} not found.");
                    }

                    // FIX 2: Fixed filter logic - get current active price (where ValidTo is null or in the future)
                    var candidatePrices = await _context.MaterialPrices
                        .Where(p => p.MaterialId == material.MaterialId && p.ValidFrom <= currentDate)
                        .ToListAsync(cancellationToken);

                    var materialPriceInDB = candidatePrices
                        .Where(p => !p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date)
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
                    _context.MaterialPrices.UpdateRange(materialPriceUpdateList);
                }

                // Update materials
                _context.Materials.UpdateRange(materialList);

                // Add new price records
                if (materialPriceCreateList.Any())
                {
                    _context.MaterialPrices.AddRange(materialPriceCreateList);
                }

                // Save all changes
                await _context.SaveChangesAsync(cancellationToken);

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
