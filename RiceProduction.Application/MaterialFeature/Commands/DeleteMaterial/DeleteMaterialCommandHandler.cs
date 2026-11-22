using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialFeature.Events;

namespace RiceProduction.Application.MaterialFeature.Commands.DeleteMaterial
{
    public class DeleteMaterialCommandHandler : IRequestHandler<DeleteMaterialCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<DeleteMaterialCommandHandler> _logger;

        public DeleteMaterialCommandHandler(IApplicationDbContext context, IMediator mediator, ILogger<DeleteMaterialCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(DeleteMaterialCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var material = await _context.Materials
                    .FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken);
                if (material == null)
                {
                    return Result<Guid>.Failure($"Material with ID {request.MaterialId} not found");
                }

                material.IsActive = false;
                _context.Materials.Update(material);
                await _context.SaveChangesAsync(cancellationToken);

                await _mediator.Publish(new MaterialChangedEvent(request.MaterialId, ChangeType.Deleted), cancellationToken);

                _logger.LogInformation("Soft deleted material with ID: {MaterialId}", request.MaterialId);
                return Result<Guid>.Success(request.MaterialId, "Material deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting material with ID: {MaterialId}", request.MaterialId);
                return Result<Guid>.Failure("Failed to delete material");
            }
        }
    }
}
