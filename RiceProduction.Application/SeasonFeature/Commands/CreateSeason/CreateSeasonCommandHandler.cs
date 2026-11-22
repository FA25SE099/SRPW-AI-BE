using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SeasonFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SeasonFeature.Commands.CreateSeason
{
    public class CreateSeasonCommandHandler : IRequestHandler<CreateSeasonCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<CreateSeasonCommandHandler> _logger;

        public CreateSeasonCommandHandler(IApplicationDbContext context, IMediator mediator, ILogger<CreateSeasonCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var exists = await _context.Seasons
                    .AnyAsync(s => s.SeasonName == request.SeasonName, cancellationToken);
                if (exists)
                {
                    return Result<Guid>.Failure($"Season with name '{request.SeasonName}' already exists");
                }

                var id = Guid.NewGuid();
                var newSeason = new Season
                {
                    Id = id,
                    SeasonName = request.SeasonName,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    SeasonType = request.SeasonType,
                    IsActive = request.IsActive
                };

                _context.Seasons.Add(newSeason);
                await _context.SaveChangesAsync(cancellationToken);

                await _mediator.Publish(new SeasonChangedEvent(id, ChangeType.Created), cancellationToken);

                _logger.LogInformation("Created season with ID: {SeasonId}", id);
                return Result<Guid>.Success(id, "Season created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating season");
                return Result<Guid>.Failure("Failed to create season");
            }
        }
    }
}

