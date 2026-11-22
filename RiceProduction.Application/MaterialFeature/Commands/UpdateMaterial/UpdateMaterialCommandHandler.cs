using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Commands.UpdateMaterial
{
    public class UpdateMaterialCommandHandler : IRequestHandler<UpdateMaterialCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<UpdateMaterialCommandHandler> _logger;

        public UpdateMaterialCommandHandler(IApplicationDbContext context, IMediator mediator, ILogger<UpdateMaterialCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateMaterialCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var material = await _context.Materials
                    .FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken);
                if (material == null)
                {
                    return Result<Guid>.Failure($"Material with ID {request.MaterialId} not found");
                }

                material.Name = request.Name;
                material.Type = request.Type;
                material.AmmountPerMaterial = request.AmmountPerMaterial;
                material.Unit = request.Unit;
                material.Description = request.Description;
                material.imgUrls = request.imgUrls;
                material.Manufacturer = request.Manufacturer;
                material.IsActive = request.IsActive;
                material.IsPartition = request.IsPartition;

                _context.Materials.Update(material);

                if (request.PricePerMaterial.HasValue)
                {
                    var currentDate = request.PriceValidFrom ?? DateTime.UtcNow;
                    var candidatePrices = await _context.MaterialPrices
                        .Where(p => p.MaterialId == request.MaterialId && p.ValidFrom <= currentDate)
                        .ToListAsync(cancellationToken);

                    var currentPrice = candidatePrices
                        .Where(p => !p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date)
                        .OrderByDescending(p => p.ValidFrom)
                        .FirstOrDefault();

                    if (currentPrice != null && currentPrice.PricePerMaterial != request.PricePerMaterial.Value)
                    {
                        currentPrice.ValidTo = currentDate;
                        _context.MaterialPrices.Update(currentPrice);

                        var newPrice = new MaterialPrice
                        {
                            MaterialId = request.MaterialId,
                            PricePerMaterial = request.PricePerMaterial.Value,
                            ValidFrom = currentDate,
                            ValidTo = null
                        };
                        _context.MaterialPrices.Add(newPrice);
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
                        _context.MaterialPrices.Add(newPrice);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

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