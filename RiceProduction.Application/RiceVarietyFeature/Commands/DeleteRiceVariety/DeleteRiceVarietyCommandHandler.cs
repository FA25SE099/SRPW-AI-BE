using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.RiceVarietyFeature.Events;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.DeleteRiceVariety
{
    public class DeleteRiceVarietyCommandHandler : IRequestHandler<DeleteRiceVarietyCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<DeleteRiceVarietyCommandHandler> _logger;

        public DeleteRiceVarietyCommandHandler(IApplicationDbContext context, IMediator mediator, ILogger<DeleteRiceVarietyCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(DeleteRiceVarietyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var variety = await _context.RiceVarieties
                    .FirstOrDefaultAsync(v => v.Id == request.RiceVarietyId, cancellationToken);
                if (variety == null)
                {
                    return Result<Guid>.Failure($"Rice variety with ID {request.RiceVarietyId} not found");
                }

                variety.IsActive = false;
                _context.RiceVarieties.Update(variety);
                await _context.SaveChangesAsync(cancellationToken);

                await _mediator.Publish(new RiceVarietyChangedEvent(request.RiceVarietyId, ChangeType.Deleted), cancellationToken);

                _logger.LogInformation("Soft deleted rice variety with ID: {RiceVarietyId}", request.RiceVarietyId);
                return Result<Guid>.Success(request.RiceVarietyId, "Rice variety deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rice variety with ID: {RiceVarietyId}", request.RiceVarietyId);
                return Result<Guid>.Failure("Failed to delete rice variety");
            }
        }
    }
}
