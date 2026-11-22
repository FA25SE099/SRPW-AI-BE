using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Commands.CreateYearSeason;

public class CreateYearSeasonCommandHandler : IRequestHandler<CreateYearSeasonCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ILogger<CreateYearSeasonCommandHandler> _logger;

    public CreateYearSeasonCommandHandler(
        IApplicationDbContext context,
        IUser user,
        ILogger<CreateYearSeasonCommandHandler> logger)
    {
        _context = context;
        _user = user;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateYearSeasonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var season = await _context.Seasons
                .AsNoTracking()
                .Where(s => s.Id == request.SeasonId)
                .Select(s => new { s.Id, s.SeasonName, s.StartDate, s.EndDate })
                .FirstOrDefaultAsync(cancellationToken);
            if (season == null)
            {
                return Result<Guid>.Failure("Season not found");
            }

            var clusterExists = await _context.Clusters
                .AnyAsync(c => c.Id == request.ClusterId, cancellationToken);
            if (!clusterExists)
            {
                return Result<Guid>.Failure("Cluster not found");
            }

            if (request.RiceVarietyId.HasValue)
            {
                var riceVarietyExists = await _context.RiceVarieties
                    .AnyAsync(rv => rv.Id == request.RiceVarietyId.Value, cancellationToken);
                if (!riceVarietyExists)
                {
                    return Result<Guid>.Failure("Rice variety not found");
                }
            }
            else if (!request.AllowFarmerSelection)
            {
                return Result<Guid>.Failure("Rice variety must be provided when farmer selection is disabled");
            }

            var duplicate = await _context.YearSeasons.AnyAsync(ys =>
                ys.ClusterId == request.ClusterId &&
                ys.SeasonId == request.SeasonId &&
                ys.Year == request.Year,
                cancellationToken);

            if (duplicate)
            {
                return Result<Guid>.Failure($"YearSeason already exists for {season.SeasonName} {request.Year} in this cluster");
            }

            // Use Season dates as default if not provided
            DateTime startDate;
            DateTime endDate;
            
            if (request.StartDate.HasValue && request.EndDate.HasValue)
            {
                // Use provided dates
                startDate = request.StartDate.Value;
                endDate = request.EndDate.Value;
                
                _logger.LogInformation(
                    "Using provided dates: {StartDate} to {EndDate}",
                    startDate, endDate);
            }
            else
            {
                // Parse from Season's MM/DD format
                startDate = ParseSeasonDate(season.StartDate, request.Year);
                endDate = ParseSeasonDate(season.EndDate, request.Year);
                
                // Handle year wraparound (e.g., Winter-Spring: 11/01 to 04/30)
                if (endDate < startDate)
                {
                    endDate = endDate.AddYears(1);
                }
                
                _logger.LogInformation(
                    "Using default dates from Season {SeasonName}: {StartDate} to {EndDate}",
                    season.SeasonName, startDate, endDate);
            }

            var userId = _user.Id;
            Guid? expertId = null;
            if (userId.HasValue)
            {
                var expertExists = await _context.AgronomyExperts
                    .AnyAsync(e => e.Id == userId.Value, cancellationToken);
                if (expertExists)
                {
                    expertId = userId.Value;
                }
            }

            var id = Guid.NewGuid();
            var newYearSeason = new YearSeason
            {
                Id = id,
                SeasonId = request.SeasonId,
                ClusterId = request.ClusterId,
                Year = request.Year,
                RiceVarietyId = request.RiceVarietyId,
                ManagedByExpertId = expertId,
                StartDate = startDate,
                EndDate = endDate,
                BreakStartDate = request.BreakStartDate,
                BreakEndDate = request.BreakEndDate,
                PlanningWindowStart = request.PlanningWindowStart,
                PlanningWindowEnd = request.PlanningWindowEnd,
                AllowFarmerSelection = request.AllowFarmerSelection,
                FarmerSelectionWindowStart = request.FarmerSelectionWindowStart,
                FarmerSelectionWindowEnd = request.FarmerSelectionWindowEnd,
                Status = SeasonStatus.Draft,
                Notes = request.Notes
            };

            _context.YearSeasons.Add(newYearSeason);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created YearSeason with ID: {YearSeasonId} for Cluster: {ClusterId}, Season: {SeasonId}, Year: {Year}",
                id, request.ClusterId, request.SeasonId, request.Year);

            return Result<Guid>.Success(id, "YearSeason created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating YearSeason");
            return Result<Guid>.Failure("Failed to create YearSeason");
        }
    }
    
    /// <summary>
    /// Parse season date from MM/DD format to actual DateTime
    /// </summary>
    private static DateTime ParseSeasonDate(string mmddString, int year)
    {
        var parts = mmddString.Split('/');
        var month = int.Parse(parts[0]);
        var day = int.Parse(parts[1]);
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }
}

