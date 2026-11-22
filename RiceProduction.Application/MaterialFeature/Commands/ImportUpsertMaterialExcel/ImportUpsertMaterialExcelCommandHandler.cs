using MediatR;
using Microsoft.EntityFrameworkCore;
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
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly IGenericExcel _genericExcel;
        private readonly ILogger<ImportUpsertMaterialExcelCommandHandler> _logger;

        public ImportUpsertMaterialExcelCommandHandler(
            IApplicationDbContext context,
            IMediator mediator,
            IGenericExcel genericExcel,
            ILogger<ImportUpsertMaterialExcelCommandHandler> logger)
        {
            _context = context;
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

                var result = new MaterialUpsertResult();
                var materialsToCreate = new List<Material>();
                var materialsToUpdate = new List<Material>();
                var priceUpdates = new List<MaterialPrice>();
                var priceCreates = new List<MaterialPrice>();

                foreach (var materialInput in materialListInput)
                {
                    var existingMaterial = await FindExistingMaterialAsync(materialInput, cancellationToken);

                    if (existingMaterial != null)
                    {
                        ApplyMaterialFields(existingMaterial, materialInput);
                        materialsToUpdate.Add(existingMaterial);

                        var candidatePrices = await _context.MaterialPrices
                            .Where(p => p.MaterialId == existingMaterial.Id && p.ValidFrom <= request.ImportDate)
                            .ToListAsync(cancellationToken);

                        var currentPrice = candidatePrices
                            .Where(p => !p.ValidTo.HasValue || p.ValidTo.Value.Date >= request.ImportDate.Date)
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
                        var newId = Guid.NewGuid();
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
                    _context.MaterialPrices.UpdateRange(priceUpdates);
                }

                if (materialsToUpdate.Any())
                {
                    _context.Materials.UpdateRange(materialsToUpdate);
                }

                if (priceCreates.Any())
                {
                    _context.MaterialPrices.AddRange(priceCreates);
                }

                if (materialsToCreate.Any())
                {
                    _context.Materials.AddRange(materialsToCreate);
                }

                await _context.SaveChangesAsync(cancellationToken);

                var allMaterials = materialsToCreate.Concat(materialsToUpdate).ToList();
                var allMaterialIds = allMaterials.Select(m => m.Id).ToList();
                var allMaterialPrices = await _context.MaterialPrices
                    .AsNoTracking()
                    .Where(p => allMaterialIds.Contains(p.MaterialId))
                    .ToListAsync(cancellationToken);
                var currentDate = DateTime.UtcNow;

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

        private async Task<Material?> FindExistingMaterialAsync(MaterialUpsertRequest materialInput, CancellationToken cancellationToken)
        {
            if (materialInput.MaterialId.HasValue && materialInput.MaterialId.Value != Guid.Empty)
            {
                var byId = await _context.Materials
                    .FirstOrDefaultAsync(m => m.Id == materialInput.MaterialId.Value, cancellationToken);
                if (byId != null)
                {
                    return byId;
                }
            }

            return await _context.Materials
                .FirstOrDefaultAsync(m =>
                    m.Name == materialInput.Name &&
                    m.Type == materialInput.Type,
                    cancellationToken);
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
                    && p.ValidFrom <= currentDate
                    && (!p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date))
                .OrderByDescending(p => p.ValidFrom)
                .FirstOrDefault()?.PricePerMaterial ?? 0;
        }
    }
}

