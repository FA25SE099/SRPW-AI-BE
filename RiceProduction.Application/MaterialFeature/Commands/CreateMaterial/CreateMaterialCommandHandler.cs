using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Commands.CreateMaterial
{
    public class CreateMaterialCommandHandler : IRequestHandler<CreateMaterialCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateMaterialCommandHandler> _logger;

        public CreateMaterialCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateMaterialCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateMaterialCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var materialRepo = _unitOfWork.Repository<Material>();
                
                var duplicate = await materialRepo.FindAsync(m => m.Name == request.Name && m.Type == request.Type);
                if (duplicate != null)
                {
                    return Result<Guid>.Failure($"Material with name '{request.Name}' and type '{request.Type}' already exists");
                }

                var id = await materialRepo.GenerateNewGuid(Guid.NewGuid());
                var newMaterial = new Material
                {
                    Id = id,
                    Name = request.Name,
                    Type = request.Type,
                    AmmountPerMaterial = request.AmmountPerMaterial,
                    Unit = request.Unit,
                    Description = request.Description,
                    Manufacturer = request.Manufacturer,
                    IsActive = request.IsActive,
                    MaterialPrices = new List<MaterialPrice>
                    {
                        new MaterialPrice
                        {
                            MaterialId = id,
                            PricePerMaterial = request.PricePerMaterial,
                            ValidFrom = request.PriceValidFrom,
                            ValidTo = null
                        }
                    }
                };

                await materialRepo.AddAsync(newMaterial);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Created material with ID: {MaterialId}", id);
                return Result<Guid>.Success(id, "Material created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating material");
                return Result<Guid>.Failure("Failed to create material");
            }
        }
    }
}

