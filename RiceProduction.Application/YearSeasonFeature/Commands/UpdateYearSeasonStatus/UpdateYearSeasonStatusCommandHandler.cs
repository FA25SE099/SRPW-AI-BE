using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Commands.UpdateYearSeasonStatus;

public class UpdateYearSeasonStatusCommandHandler : IRequestHandler<UpdateYearSeasonStatusCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateYearSeasonStatusCommandHandler> _logger;

    public UpdateYearSeasonStatusCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdateYearSeasonStatusCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateYearSeasonStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var yearSeason = await _context.YearSeasons
                .FirstOrDefaultAsync(ys => ys.Id == request.Id, cancellationToken);

            if (yearSeason == null)
            {
                return Result<bool>.Failure("YearSeason not found");
            }

            yearSeason.Status = request.Status;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated YearSeason status to {Status} for ID: {YearSeasonId}", request.Status, request.Id);
            return Result<bool>.Success(true, $"YearSeason status updated to {request.Status}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating YearSeason status");
            return Result<bool>.Failure("Failed to update YearSeason status");
        }
    }
}

