using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.RiceVarietyFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.CreateRiceVariety
{
    public class CreateRiceVarietyCommandHandler : IRequestHandler<CreateRiceVarietyCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<CreateRiceVarietyCommandHandler> _logger;

        public CreateRiceVarietyCommandHandler(IApplicationDbContext context, IMediator mediator, ILogger<CreateRiceVarietyCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateRiceVarietyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var categoryExists = await _context.RiceVarietyCategories
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
                if (!categoryExists)
                {
                    return Result<Guid>.Failure($"Category with ID {request.CategoryId} not found");
                }

                var duplicateExists = await _context.RiceVarieties
                    .AsNoTracking()
                    .AnyAsync(v => v.VarietyName == request.VarietyName, cancellationToken);
                if (duplicateExists)
                {
                    return Result<Guid>.Failure($"Rice variety with name '{request.VarietyName}' already exists");
                }

                var id = Guid.NewGuid();
                var newVariety = new RiceVariety
                {
                    Id = id,
                    VarietyName = request.VarietyName,
                    CategoryId = request.CategoryId,
                    BaseGrowthDurationDays = request.BaseGrowthDurationDays,
                    BaseYieldPerHectare = request.BaseYieldPerHectare,
                    Description = request.Description,
                    Characteristics = request.Characteristics,
                    IsActive = request.IsActive
                };

                _context.RiceVarieties.Add(newVariety);
                await _context.SaveChangesAsync(cancellationToken);

                await _mediator.Publish(new RiceVarietyChangedEvent(id, ChangeType.Created), cancellationToken);

                _logger.LogInformation("Created rice variety with ID: {RiceVarietyId}", id);
                return Result<Guid>.Success(id, "Rice variety created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rice variety");
                return Result<Guid>.Failure("Failed to create rice variety");
            }
        }
    }
}

