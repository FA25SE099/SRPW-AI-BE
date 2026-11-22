using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.RiceVarietyFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.UpdateRiceVariety
{
    public class UpdateRiceVarietyCommandHandler : IRequestHandler<UpdateRiceVarietyCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<UpdateRiceVarietyCommandHandler> _logger;

        public UpdateRiceVarietyCommandHandler(IApplicationDbContext context, IMediator mediator, ILogger<UpdateRiceVarietyCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateRiceVarietyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var variety = await _context.RiceVarieties
                    .FirstOrDefaultAsync(v => v.Id == request.RiceVarietyId, cancellationToken);
                if (variety == null)
                {
                    return Result<Guid>.Failure($"Rice variety with ID {request.RiceVarietyId} not found");
                }

                var categoryExists = await _context.RiceVarietyCategories
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
                if (!categoryExists)
                {
                    return Result<Guid>.Failure($"Category with ID {request.CategoryId} not found");
                }

                variety.VarietyName = request.VarietyName;
                variety.CategoryId = request.CategoryId;
                variety.BaseGrowthDurationDays = request.BaseGrowthDurationDays;
                variety.BaseYieldPerHectare = request.BaseYieldPerHectare;
                variety.Description = request.Description;
                variety.Characteristics = request.Characteristics;
                variety.IsActive = request.IsActive;

                _context.RiceVarieties.Update(variety);
                await _context.SaveChangesAsync(cancellationToken);

                await _mediator.Publish(new RiceVarietyChangedEvent(request.RiceVarietyId, ChangeType.Updated), cancellationToken);

                _logger.LogInformation("Updated rice variety with ID: {RiceVarietyId}", request.RiceVarietyId);
                return Result<Guid>.Success(request.RiceVarietyId, "Rice variety updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rice variety with ID: {RiceVarietyId}", request.RiceVarietyId);
                return Result<Guid>.Failure("Failed to update rice variety");
            }
        }
    }
}

