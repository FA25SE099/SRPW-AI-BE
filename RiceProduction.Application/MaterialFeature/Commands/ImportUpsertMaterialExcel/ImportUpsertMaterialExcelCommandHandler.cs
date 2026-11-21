using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.MaterialFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportUpsertMaterialExcel
{
    public class ImportUpsertMaterialExcelCommandHandler : IRequestHandler<ImportUpsertMaterialExcelCommand, Result<MaterialUpsertResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<ImportUpsertMaterialExcelCommandHandler> _logger;

        public ImportUpsertMaterialExcelCommandHandler(
            IUnitOfWork unitOfWork,
            IMediator mediator,
            IGenericExcel genericExcel,
            ILogger<ImportUpsertMaterialExcelCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mediator = mediator;
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

                var validationErrors = ValidateMaterials(materialListInput);
                if (validationErrors.Any())
                {
                    return Result<MaterialUpsertResult>.Failure(
                        $"Validation failed:{string.Join("\n", validationErrors)}");
                }

                var materialRepo = _unitOfWork.Repository<Material>();
                var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();

                var result = new MaterialUpsertResult();
                var materialsToCreate = new List<Material>();
                var materialsToUpdate = new List<Material>();
                var priceUpdates = new List<MaterialPrice>();
                var priceCreates = new List<MaterialPrice>();

                foreach (var materialInput in materialListInput)
                {
                    var existingMaterial = await FindExistingMaterialAsync(materialInput);

                    if (existingMaterial != null)
                    {
                        ApplyMaterialFields(existingMaterial, materialInput);
                        materialsToUpdate.Add(existingMaterial);

                        var currentPrice = (await materialPriceRepo.ListAsync(
                            p => p.MaterialId == existingMaterial.Id &&
                                 p.ValidFrom.CompareTo(request.ImportDate) <= 0 &&
                                 (!p.ValidTo.HasValue || p.ValidTo.Value.Date.CompareTo(request.ImportDate) >= 0)))
                            .OrderByDescending(p => p.ValidFrom)
                            .FirstOrDefault();

                        if (currentPrice == null || currentPrice.PricePerMaterial != materialInput.PricePerMaterial)
                        {
                            if (currentPrice != null)
                            {
                                currentPrice.ValidTo = request.ImportDate;
                                priceUpdates.Add(currentPrice);
                            }

                            priceCreates.Add(CreateMaterialPrice(existingMaterial.Id, materialInput.PricePerMaterial, request.ImportDate));
                        }

                        result.UpdatedCount++;
                    }
                    else
                    {
                        var newId = await materialRepo.GenerateNewGuid(Guid.NewGuid());
                        var newMaterial = new Material
                        {
                            Id = newId,
                            MaterialPrices = new List<MaterialPrice>
                            {
                                CreateMaterialPrice(newId, materialInput.PricePerMaterial, request.ImportDate)
                            }
                        };
                        ApplyMaterialFields(newMaterial, materialInput);

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
                var allMaterialPrices = await materialPriceRepo.ListAsync();
                var currentDate = DateTime.Now;

                result.Materials = allMaterials.Select(m => new MaterialResponse
                {
                    MaterialId = m.Id,
                    Name = m.Name,
                    Type = m.Type,
                    AmmountPerMaterial = m.AmmountPerMaterial,
                    Showout = m.AmmountPerMaterial.ToString() + m.Unit,
                    PricePerMaterial = GetActivePrice(m, allMaterialPrices, currentDate),
                    Unit = m.Unit,
                    Description = m.Description,
                    Manufacturer = m.Manufacturer,
                    IsActive = m.IsActive
                }).ToList();

                await _mediator.Publish(new MaterialChangedEvent(null, ChangeType.BulkImport), cancellationToken);

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

        private static List<string> ValidateMaterials(IList<MaterialUpsertRequest> materials)
        {
            var validationErrors = new List<string>();
            for (int i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
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

            return validationErrors;
        }

        private async Task<Material?> FindExistingMaterialAsync(MaterialUpsertRequest materialInput)
        {
            var materialRepo = _unitOfWork.Repository<Material>();

            if (materialInput.MaterialId.HasValue && materialInput.MaterialId.Value != Guid.Empty)
            {
                var byId = await materialRepo.FindAsync(m => m.Id == materialInput.MaterialId.Value);
                if (byId != null)
                {
                    return byId;
                }
            }

            return await materialRepo.FindAsync(m =>
                m.Name == materialInput.Name &&
                m.Type == materialInput.Type);
        }

        private static void ApplyMaterialFields(Material target, MaterialUpsertRequest source)
        {
            target.Name = source.Name;
            target.Type = source.Type;
            target.AmmountPerMaterial = source.AmmountPerMaterial;
            target.Unit = source.Unit;
            target.Description = source.Description;
            target.Manufacturer = source.Manufacturer;
            target.IsActive = source.IsActive;
        }

        private static MaterialPrice CreateMaterialPrice(Guid materialId, decimal pricePerMaterial, DateTime validFrom)
        {
            return new MaterialPrice
            {
                MaterialId = materialId,
                PricePerMaterial = pricePerMaterial,
                ValidFrom = validFrom,
                ValidTo = null
            };
        }

        private static decimal GetActivePrice(Material material, IEnumerable<MaterialPrice> prices, DateTime currentDate)
        {
            if (!material.IsActive)
            {
                return 0;
            }

            return prices
                .Where(p => p.MaterialId == material.Id
                    && p.ValidFrom.CompareTo(currentDate) <= 0
                    && (!p.ValidTo.HasValue || p.ValidTo.Value.Date.CompareTo(currentDate) >= 0))
                .OrderByDescending(p => p.ValidFrom)
                .FirstOrDefault()?.PricePerMaterial ?? 0;
        }
    }
}

