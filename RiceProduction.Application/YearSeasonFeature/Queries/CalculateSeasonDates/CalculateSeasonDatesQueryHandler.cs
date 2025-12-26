using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Queries.CalculateSeasonDates;

public class CalculateSeasonDatesQueryHandler : IRequestHandler<CalculateSeasonDatesQuery, Result<SeasonDatesDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateSeasonDatesQueryHandler> _logger;

    public CalculateSeasonDatesQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<CalculateSeasonDatesQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SeasonDatesDto>> Handle(
        CalculateSeasonDatesQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get season
            var season = await _unitOfWork.Repository<Season>().GetEntityByIdAsync(request.SeasonId);
            if (season == null)
            {
                return Result<SeasonDatesDto>.Failure("Season not found");
            }

            // Validate year
            if (request.Year < 2000 || request.Year > 2100)
            {
                return Result<SeasonDatesDto>.Failure("Year must be between 2000 and 2100");
            }

            // Parse season dates (DD/MM format)
            var startDate = ParseSeasonDate(season.StartDate, request.Year);
            var endDate = ParseSeasonDate(season.EndDate, request.Year);
            
            // Check if season crosses years
            bool crossesYears = false;
            if (endDate < startDate)
            {
                endDate = endDate.AddYears(1);
                crossesYears = true;
            }

            // Calculate duration
            var durationDays = (int)(endDate - startDate).TotalDays;

            // Calculate suggested windows
            var now = DateTime.UtcNow;
            
            // Suggested farmer selection window (30 days before season, ends 7 days before)
            var suggestedFarmerSelectionStart = startDate.AddDays(-30);
            var suggestedFarmerSelectionEnd = startDate.AddDays(-7);
            
            // Suggested planning window (for BOTH group formation + production planning)
            // Starts right after farmer selection ends, ends 3 days before season
            var suggestedPlanningStart = startDate.AddDays(-6); // Day after selection ends
            var suggestedPlanningEnd = startDate.AddDays(-3);   // 3 days before season

            var dto = new SeasonDatesDto
            {
                SeasonId = season.Id,
                SeasonName = season.SeasonName,
                Year = request.Year,
                SeasonStartDateFormat = season.StartDate,
                SeasonEndDateFormat = season.EndDate,
                StartDate = startDate,
                EndDate = endDate,
                CrossesYears = crossesYears,
                DurationDays = durationDays,
                SuggestedFarmerSelectionWindowStart = suggestedFarmerSelectionStart,
                SuggestedFarmerSelectionWindowEnd = suggestedFarmerSelectionEnd,
                SuggestedPlanningWindowStart = suggestedPlanningStart,
                SuggestedPlanningWindowEnd = suggestedPlanningEnd
            };

            _logger.LogInformation(
                "Calculated dates for Season {SeasonName} Year {Year}: {StartDate} to {EndDate} (crosses years: {CrossesYears})",
                season.SeasonName, request.Year, startDate, endDate, crossesYears);

            return Result<SeasonDatesDto>.Success(dto, "Season dates calculated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating season dates for Season {SeasonId} Year {Year}",
                request.SeasonId, request.Year);
            return Result<SeasonDatesDto>.Failure($"Failed to calculate season dates: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse season date from DD/MM format to actual DateTime
    /// </summary>
    private static DateTime ParseSeasonDate(string ddmmString, int year)
    {
        var parts = ddmmString.Split('/');
        
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid date format: {ddmmString}. Expected DD/MM format.");
        }
        
        if (!int.TryParse(parts[0], out var day) || day < 1 || day > 31)
        {
            throw new ArgumentException($"Invalid day in date: {ddmmString}");
        }
        
        if (!int.TryParse(parts[1], out var month) || month < 1 || month > 12)
        {
            throw new ArgumentException($"Invalid month in date: {ddmmString}");
        }
        
        return new DateTime(year, month, day);
    }
}

