using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportCreateAllMaterialExcel
{
    public class ImportCreateAllMaterialExcelCommandHandler : IRequestHandler<ImportCreateAllMaterialExcelCommand,
        Result<List<MaterialResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;

        public ImportCreateAllMaterialExcelCommandHandler(IUnitOfWork unitOfWork, IGenericExcel genericExcel)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
        }

        public async Task<Result<List<MaterialResponse>>> Handle(ImportCreateAllMaterialExcelCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var materialListCreateInput =
                    await _genericExcel.ExcelToListT<MaterialCreateRequest>(request.ExcelFile);
                if (materialListCreateInput == null || !materialListCreateInput.Any())
                {
                    return Result<List<MaterialResponse>>.Failure("The uploaded Excel file is empty or invalid.");
                }

                var validationErrors = ValidateMaterials(materialListCreateInput);
                if (validationErrors.Any())
                {
                    return Result<List<MaterialResponse>>.Failure(
                        $"Excel validation failed:\n{string.Join("\n", validationErrors)}");
                }

                var duplicateName = await FindDuplicateMaterialNameAsync(materialListCreateInput);
                if (duplicateName != null)
                {
                    return Result<List<MaterialResponse>>.Failure(
                        $"The uploaded Excel file contain duplicate material in the system, material name {duplicateName}. Please check again!");
                }

                var materialRepo = _unitOfWork.Repository<Material>();
                var materialList = new List<Material>();
                foreach (var material in materialListCreateInput)
                {
                    var id = await materialRepo.GenerateNewGuid(Guid.NewGuid());
                    materialList.Add(CreateMaterialEntity(id, material, request.ImportDate));
                }

                await materialRepo.AddRangeAsync(materialList);

                var result = await _unitOfWork.CompleteAsync();
                if (result <= 0)
                {
                    return Result<List<MaterialResponse>>.Failure("Failed to import materials.");
                }

                var materialCreateSuccessList = await BuildMaterialResponsesAsync(materialList);

                return Result<List<MaterialResponse>>.Success(
                    materialCreateSuccessList,
                    $"Successfully created {materialCreateSuccessList.Count} materials!");
            }
            catch (Exception ex)
            {
                return Result<List<MaterialResponse>>.Failure(
                    $"An error occurred while importing materials: {ex.Message}");
            }
        }

        private static List<string> ValidateMaterials(List<MaterialCreateRequest> materials)
        {
            var validationErrors = new List<string>();

            for (int i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                var rowNumber = i + 2; // Excel row number (accounting for header)

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
                    validationErrors.Add($"Row {rowNumber}: Price per material must be greater than 0");
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

        private async Task<string?> FindDuplicateMaterialNameAsync(List<MaterialCreateRequest> materials)
        {
            var materialRepo = _unitOfWork.Repository<Material>();

            foreach (var material in materials)
            {
                var duplicate = await materialRepo.FindAsync(m =>
                    m.Name == material.Name &&
                    m.Type == material.Type &&
                    m.AmmountPerMaterial == material.AmmountPerMaterial &&
                    m.Unit == material.Unit &&
                    m.Description == material.Description &&
                    m.Manufacturer == material.Manufacturer);

                if (duplicate != null)
                {
                    return material.Name;
                }
            }

            return null;
        }

        private static Material CreateMaterialEntity(Guid id, MaterialCreateRequest input, DateTime importDate) =>
            new()
            {
                Id = id,
                Name = input.Name,
                Type = input.Type,
                AmmountPerMaterial = input.AmmountPerMaterial,
                Unit = input.Unit,
                Description = input.Description,
                Manufacturer = input.Manufacturer,
                IsActive = input.IsActive,
                MaterialPrices = new List<MaterialPrice>
                {
                    new MaterialPrice
                    {
                        MaterialId = id,
                        PricePerMaterial = input.PricePerMaterial,
                        ValidFrom = importDate,
                        ValidTo = null
                    }
                },
                IsPartition = false
            };

        private async Task<List<MaterialResponse>> BuildMaterialResponsesAsync(List<Material> materials)
        {
            var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();
            var responses = new List<MaterialResponse>();

            foreach (var material in materials)
            {
                var prices = await materialPriceRepo.ListAsync(p => p.MaterialId == material.Id);
                var latestPrice = prices
                    .OrderByDescending(p => p.ValidFrom)
                    .FirstOrDefault()?.PricePerMaterial ?? 0;

                responses.Add(new MaterialResponse
                {
                    MaterialId = material.Id,
                    Name = material.Name,
                    Type = material.Type,
                    AmmountPerMaterial = material.AmmountPerMaterial,
                    Showout = material.AmmountPerMaterial.ToString() + material.Unit,
                    PricePerMaterial = latestPrice,
                    Unit = material.Unit,
                    Description = material.Description,
                    Manufacturer = material.Manufacturer,
                    IsActive = material.IsActive
                });
            }

            return responses;
        }
    }
}
