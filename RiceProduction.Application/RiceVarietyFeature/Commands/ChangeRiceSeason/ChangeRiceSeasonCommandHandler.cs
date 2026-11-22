using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
namespace RiceProduction.Application.RiceVarietyFeature.Commands.ChangeRiceSeason;

public class ChangeRiceSeasonCommandHandler : IRequestHandler<ChangeRiceSeasonCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ChangeRiceSeasonCommandHandler> _logger;

    public ChangeRiceSeasonCommandHandler(IApplicationDbContext context, ILogger<ChangeRiceSeasonCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(ChangeRiceSeasonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var alreadyExists = await _context.RiceVarietySeasons
                .AsNoTracking()
                .AnyAsync(
                    rs => rs.RiceVarietyId == request.RiceId && rs.SeasonId == request.SeasonId,
                    cancellationToken);
            if (alreadyExists)
            {
                return Result<Guid>.Failure($"Already matching rice {request.RiceId} and season {request.SeasonId}");
            }

            var newRiceSeason = new RiceVarietySeason
            {
                Id = Guid.NewGuid(),
                RiceVarietyId = request.RiceId,
                SeasonId = request.SeasonId
            };
            _context.RiceVarietySeasons.Add(newRiceSeason);
            return await _context.SaveChangesAsync(cancellationToken) == 1
                ? Result<Guid>.Success(newRiceSeason.Id, "Successfully matched rice with season.")
                : Result<Guid>.Failure("Failed to match rice with season.", "ChangeRiceSeasonFailed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching rice {RiceId} with season {SeasonId}", request.RiceId, request.SeasonId);

            return Result<Guid>.Failure("An error occurred during change rice season process.", "ChangeRiceSeasonFailed");
        }
    }

}