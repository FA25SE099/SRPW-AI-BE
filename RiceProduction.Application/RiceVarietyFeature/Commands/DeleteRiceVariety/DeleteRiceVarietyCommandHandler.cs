using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.RiceVarietyFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.DeleteRiceVariety
{
    public class DeleteRiceVarietyCommandHandler : IRequestHandler<DeleteRiceVarietyCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        private readonly ILogger<DeleteRiceVarietyCommandHandler> _logger;

        public DeleteRiceVarietyCommandHandler(IUnitOfWork unitOfWork, IMediator mediator, ILogger<DeleteRiceVarietyCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(DeleteRiceVarietyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietyRepo = _unitOfWork.Repository<RiceVariety>();

                var variety = await riceVarietyRepo.FindAsync(v => v.Id == request.RiceVarietyId);
                if (variety == null)
                {
                    return Result<Guid>.Failure($"Rice variety with ID {request.RiceVarietyId} not found");
                }

                variety.IsActive = false;
                riceVarietyRepo.Update(variety);
                await _unitOfWork.CompleteAsync();

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

