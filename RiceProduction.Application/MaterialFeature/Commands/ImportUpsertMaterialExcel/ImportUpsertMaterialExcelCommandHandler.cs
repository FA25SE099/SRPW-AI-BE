using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportUpsertMaterialExcel
{
    public class ImportUpsertMaterialExcelCommandHandler : IRequestHandler<ImportUpsertMaterialExcelCommand, Result<MaterialUpsertResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<ImportUpsertMaterialExcelCommandHandler> _logger;

        public ImportUpsertMaterialExcelCommandHandler(
            IUnitOfWork unitOfWork, 
            IGenericExcel genericExcel,
            ILogger<ImportUpsertMaterialExcelCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
            _logger = logger;
        }

        public async Task<Result<MaterialUpsertResult>> Handle(ImportUpsertMaterialExcelCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var materialListInput = await _genericExcel.ExcelToListT<MaterialUpsertRequest>(request.ExcelFile);
                if (materialListInput == null || !materialListInput.Any())
                {
                    return Result<MaterialUpsertResult>.Failure("The uploaded Excel file is empty or invalid.");
                }

                var materialRepo = _unitOfWork.Repository<Material>();
                var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();

                var result = new MaterialUpsertResult();
                var materialsToCreate = new List<Material>();
                var materialsToUpdate = new List<Material>();
                var priceUpdates = new List<MaterialPrice>();
                var priceCreates = new List<MaterialPrice>();

                var validationErrors = new List<string>();
                for (int i = 0; i < materialListInput.Count; i++)
                {
                    var material = materialListInput[i];
                    var rowNumber = i + 2;

                    if (string.IsNullOrWhiteSpace(material.Name))
                    {
                        validationErrors.Add($"Row {rowNumber}: Material name is required");
                    }
                    else if (material.Name.Length > 255)
                    {
                        validationErrors.Add($"Row {rowNumber}: Material name must not exceed 255 characters");
                    }

                    if (string.IsNullOrWhiteSpace(material.Unit))
                    {
                        validationErrors.Add($"Row {rowNumber}: Unit is required");
                    }
                    else if (material.Unit.Length > 50)
                    {
                        validationErrors.Add($"Row {rowNumber}: Unit must not exceed 50 characters");
                    }

                    if (material.PricePerMaterial <= 0)
                    {
                        validationErrors.Add($"Row {rowNumber}: Price must be greater than 0");
                    }

                    if (material.AmmountPerMaterial.HasValue && material.AmmountPerMaterial.Value <= 0)
                    {
                        validationErrors.Add($"Row {rowNumber}: Amount per material must be greater than 0");
                    }

                    if (!string.IsNullOrWhiteSpace(material.Manufacturer) && material.Manufacturer.Length > 255)
                    {
                        validationErrors.Add($"Row {rowNumber}: Manufacturer must not exceed 255 characters");
                    }
                }

                if (validationErrors.Any())
                {
                    return Result<MaterialUpsertResult>.Failure(
                        $"Validation failed:{string.Join("\n", validationErrors)}");
                }

                foreach (var materialInput in materialListInput)
                {
                    Material existingMaterial = null;

                    if (materialInput.MaterialId.HasValue && materialInput.MaterialId.Value != Guid.Empty)
                    {
                        existingMaterial = await materialRepo.FindAsync(m => m.Id == materialInput.MaterialId.Value);
                    }

                    if (existingMaterial == null)
                    {
                        existingMaterial = await materialRepo.FindAsync(m => 
                            m.Name == materialInput.Name && 
                            m.Type == materialInput.Type);
                    }

                    if (existingMaterial != null)
                    {
                        existingMaterial.Name = materialInput.Name;
                        existingMaterial.Type = materialInput.Type;
                        existingMaterial.AmmountPerMaterial = materialInput.AmmountPerMaterial;
                        existingMaterial.Unit = materialInput.Unit;
                        existingMaterial.Description = materialInput.Description;
                        existingMaterial.Manufacturer = materialInput.Manufacturer;
                        existingMaterial.IsActive = materialInput.IsActive;

                        materialsToUpdate.Add(existingMaterial);

                        var currentPrice = (await materialPriceRepo.ListAsync(
                            p => p.MaterialId == existingMaterial.Id &&
                                 (p.ValidFrom.CompareTo(request.ImportDate) <= 0) &&
                                 (!p.ValidTo.HasValue || (p.ValidTo.Value.Date.CompareTo(request.ImportDate) >= 0))))
                            .OrderByDescending(p => p.ValidFrom)
                            .FirstOrDefault();

                        if (currentPrice != null && currentPrice.PricePerMaterial != materialInput.PricePerMaterial)
                        {
                            currentPrice.ValidTo = request.ImportDate;
                            priceUpdates.Add(currentPrice);

                            priceCreates.Add(new MaterialPrice
                            {
                                MaterialId = existingMaterial.Id,
                                PricePerMaterial = materialInput.PricePerMaterial,
                                ValidFrom = request.ImportDate,
                                ValidTo = null
                            });
                        }
                        else if (currentPrice == null)
                        {
                            priceCreates.Add(new MaterialPrice
                            {
                                MaterialId = existingMaterial.Id,
                                PricePerMaterial = materialInput.PricePerMaterial,
                                ValidFrom = request.ImportDate,
                                ValidTo = null
                            });
                        }

                        result.UpdatedCount++;
                    }
                    else
                    {
                        var newId = await materialRepo.GenerateNewGuid(Guid.NewGuid());
                        var newMaterial = new Material
                        {
                            Id = newId,
                            Name = materialInput.Name,
                            Type = materialInput.Type,
                            AmmountPerMaterial = materialInput.AmmountPerMaterial,
                            Unit = materialInput.Unit,
                            Description = materialInput.Description,
                            Manufacturer = materialInput.Manufacturer,
                            IsActive = materialInput.IsActive,
                            MaterialPrices = new List<MaterialPrice>
                            {
                                new MaterialPrice
                                {
                                    MaterialId = newId,
                                    PricePerMaterial = materialInput.PricePerMaterial,
                                    ValidFrom = request.ImportDate,
                                    ValidTo = null
                                }
                            }
                        };

                        materialsToCreate.Add(newMaterial);
                        result.CreatedCount++;
                    }
                }

                if (priceUpdates.Any())
                {
                    materialPriceRepo.UpdateRange(priceUpdates);
                }

                if (materialsToUpdate.Any())
                {
                    materialRepo.UpdateRange(materialsToUpdate);
                }

                if (priceCreates.Any())
                {
                    await materialPriceRepo.AddRangeAsync(priceCreates);
                }

                if (materialsToCreate.Any())
                {
                    await materialRepo.AddRangeAsync(materialsToCreate);
                }

                await _unitOfWork.CompleteAsync();

                var allMaterials = materialsToCreate.Concat(materialsToUpdate).ToList();
                var materialPriceRepoList = await materialPriceRepo.ListAsync();
                var currentDate = DateTime.Now;

                result.Materials = allMaterials.Select(m => new MaterialResponse
                {
                    MaterialId = m.Id,
                    Name = m.Name,
                    Type = m.Type,
                    AmmountPerMaterial = m.AmmountPerMaterial,
                    Showout = m.AmmountPerMaterial.ToString() + m.Unit,
                    PricePerMaterial = materialPriceRepoList
                        .Where(p => p.MaterialId == m.Id
                            && m.IsActive
                            && (p.ValidFrom.CompareTo(currentDate) <= 0)
                            && (!p.ValidTo.HasValue || (p.ValidTo.Value.Date.CompareTo(currentDate) >= 0)))
                        .OrderByDescending(p => p.ValidFrom)
                        .FirstOrDefault()?.PricePerMaterial ?? 0,
                    Unit = m.Unit,
                    Description = m.Description,
                    Manufacturer = m.Manufacturer,
                    IsActive = m.IsActive
                }).ToList();

                _logger.LogInformation(
                    "Material upsert completed: Created={Created}, Updated={Updated}", 
                    result.CreatedCount, 
                    result.UpdatedCount);

                return Result<MaterialUpsertResult>.Success(
                    result,
                    $"Successfully processed {result.CreatedCount + result.UpdatedCount} materials: {result.CreatedCount} created, {result.UpdatedCount} updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during material upsert");
                return Result<MaterialUpsertResult>.Failure($"An error occurred: {ex.Message}");
            }
        }
    }
}

