using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Commands.UpdateMaterial
{
    public class UpdateMaterialCommandHandler : IRequestHandler<UpdateMaterialCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        private readonly ILogger<UpdateMaterialCommandHandler> _logger;

        public UpdateMaterialCommandHandler(IUnitOfWork unitOfWork, IMediator mediator, ILogger<UpdateMaterialCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateMaterialCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var materialRepo = _unitOfWork.Repository<Material>();
                var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();

                var material = await materialRepo.FindAsync(m => m.Id == request.MaterialId);
                if (material == null)
                {
                    return Result<Guid>.Failure($"Material with ID {request.MaterialId} not found");
                }

                material.Name = request.Name;
                material.Type = request.Type;
                material.AmmountPerMaterial = request.AmmountPerMaterial;
                material.Unit = request.Unit;
                material.Description = request.Description;
                material.Manufacturer = request.Manufacturer;
                material.IsActive = request.IsActive;

                materialRepo.Update(material);

                if (request.PricePerMaterial.HasValue)
                {
                    var currentDate = request.PriceValidFrom ?? DateTime.UtcNow;
                    var currentPrice = (await materialPriceRepo.ListAsync(
                        p => p.MaterialId == request.MaterialId &&
                             (p.ValidFrom.CompareTo(currentDate) <= 0) &&
                             (!p.ValidTo.HasValue || (p.ValidTo.Value.Date.CompareTo(currentDate) >= 0))))
                        .OrderByDescending(p => p.ValidFrom)
                        .FirstOrDefault();

                    if (currentPrice != null && currentPrice.PricePerMaterial != request.PricePerMaterial.Value)
                    {
                        currentPrice.ValidTo = currentDate;
                        materialPriceRepo.Update(currentPrice);

                        var newPrice = new MaterialPrice
                        {
                            MaterialId = request.MaterialId,
                            PricePerMaterial = request.PricePerMaterial.Value,
                            ValidFrom = currentDate,
                            ValidTo = null
                        };
                        await materialPriceRepo.AddAsync(newPrice);
                    }
                    else if (currentPrice == null)
                    {
                        var newPrice = new MaterialPrice
                        {
                            MaterialId = request.MaterialId,
                            PricePerMaterial = request.PricePerMaterial.Value,
                            ValidFrom = currentDate,
                            ValidTo = null
                        };
                        await materialPriceRepo.AddAsync(newPrice);
                    }
                }

                await _unitOfWork.CompleteAsync();

                await _mediator.Publish(new MaterialChangedEvent(request.MaterialId, ChangeType.Updated), cancellationToken);

                _logger.LogInformation("Updated material with ID: {MaterialId}", request.MaterialId);
                return Result<Guid>.Success(request.MaterialId, "Material updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating material with ID: {MaterialId}", request.MaterialId);
                return Result<Guid>.Failure("Failed to update material");
            }
        }
    }
}

