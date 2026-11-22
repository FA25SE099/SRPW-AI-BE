using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SeasonFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SeasonFeature.Commands.DeleteSeason
{
    public class DeleteSeasonCommandHandler : IRequestHandler<DeleteSeasonCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<DeleteSeasonCommandHandler> _logger;

        public DeleteSeasonCommandHandler(IApplicationDbContext context, IMediator mediator, ILogger<DeleteSeasonCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(DeleteSeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.Id == request.SeasonId, cancellationToken);
                if (season == null)
                {
                    return Result<Guid>.Failure($"Season with ID {request.SeasonId} not found");
                }

                season.IsActive = false;
                _context.Seasons.Update(season);
                await _context.SaveChangesAsync(cancellationToken);

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

