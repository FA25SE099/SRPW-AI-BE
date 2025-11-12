using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SeasonFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SeasonFeature.Commands.DeleteSeason
{
    public class DeleteSeasonCommandHandler : IRequestHandler<DeleteSeasonCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        private readonly ILogger<DeleteSeasonCommandHandler> _logger;

        public DeleteSeasonCommandHandler(IUnitOfWork unitOfWork, IMediator mediator, ILogger<DeleteSeasonCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(DeleteSeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var seasonRepo = _unitOfWork.Repository<Season>();

                var season = await seasonRepo.FindAsync(s => s.Id == request.SeasonId);
                if (season == null)
                {
                    return Result<Guid>.Failure($"Season with ID {request.SeasonId} not found");
                }

                season.IsActive = false;
                seasonRepo.Update(season);
                await _unitOfWork.CompleteAsync();

                await _mediator.Publish(new SeasonChangedEvent(request.SeasonId, ChangeType.Deleted), cancellationToken);

                _logger.LogInformation("Soft deleted season with ID: {SeasonId}", request.SeasonId);
                return Result<Guid>.Success(request.SeasonId, "Season deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting season with ID: {SeasonId}", request.SeasonId);
                return Result<Guid>.Failure("Failed to delete season");
            }
        }
    }
}

