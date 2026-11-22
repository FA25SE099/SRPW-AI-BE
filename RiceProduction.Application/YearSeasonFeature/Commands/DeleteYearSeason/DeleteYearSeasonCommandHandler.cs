using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Commands.DeleteYearSeason;

public class DeleteYearSeasonCommandHandler : IRequestHandler<DeleteYearSeasonCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DeleteYearSeasonCommandHandler> _logger;

    public DeleteYearSeasonCommandHandler(
        IApplicationDbContext context,
        ILogger<DeleteYearSeasonCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteYearSeasonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var yearSeason = await _context.YearSeasons
                .FirstOrDefaultAsync(ys => ys.Id == request.Id, cancellationToken);

            if (yearSeason == null)
            {
                return Result<bool>.Failure("YearSeason not found");
            }

            var hasGroups = await _context.Groups
                .AnyAsync(g => g.YearSeasonId == request.Id, cancellationToken);

            if (hasGroups)
            {
                return Result<bool>.Failure("Cannot delete YearSeason that has associated groups");
            }

            _context.YearSeasons.Remove(yearSeason);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted YearSeason with ID: {YearSeasonId}", request.Id);
            return Result<bool>.Success(true, "YearSeason deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting YearSeason");
            return Result<bool>.Failure("Failed to delete YearSeason");
        }
    }
}

