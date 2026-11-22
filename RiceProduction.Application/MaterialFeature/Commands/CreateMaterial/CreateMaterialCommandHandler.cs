using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialFeature.Commands.CreateMaterial
{
    public class CreateMaterialCommandHandler : IRequestHandler<CreateMaterialCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<CreateMaterialCommandHandler> _logger;

        public CreateMaterialCommandHandler(IApplicationDbContext context, IMediator mediator, ILogger<CreateMaterialCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateMaterialCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var duplicateExists = await _context.Materials
                    .AsNoTracking()
                    .AnyAsync(m => m.Name == request.Name && m.Type == request.Type, cancellationToken);
                if (duplicateExists)
                {
                    return Result<Guid>.Failure($"Material with name '{request.Name}' and type '{request.Type}' already exists");
                }

                var id = Guid.NewGuid();
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

                _context.Materials.Add(newMaterial);
                await _context.SaveChangesAsync(cancellationToken);

                await _mediator.Publish(new MaterialChangedEvent(id, ChangeType.Created), cancellationToken);

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

