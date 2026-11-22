using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Queries.GetCurrentSeason;

public class GetCurrentSeasonQueryHandler
    : IRequestHandler<GetCurrentSeasonQuery, Result<CurrentSeasonInfo>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetCurrentSeasonQueryHandler> _logger;

    public GetCurrentSeasonQueryHandler(
        IApplicationDbContext context,
        ILogger<GetCurrentSeasonQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<CurrentSeasonInfo>> Handle(
        GetCurrentSeasonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.Now;
            var allSeasons = await _context.Seasons
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var season in allSeasons)
            {
                if (!SeasonDateHelper.IsDateInSeasonRange(today.Month, today.Day, season.StartDate, season.EndDate))
                {
                    continue;
                }

                SeasonDateHelper.TryParseMonthDay(season.StartDate, out var startMonth, out var startDay);
                int year = today.Month < startMonth && startMonth > 6 ? today.Year - 1 : today.Year;

                var seasonStartDate = new DateTime(year, startMonth, startDay);
                var daysIntoSeason = (int)(today - seasonStartDate).TotalDays;

                var response = new CurrentSeasonInfo
                {
                    SeasonId = season.Id,
                    SeasonName = season.SeasonName,
                    SeasonType = season.SeasonType ?? "",
                    Year = year,
                    StartDate = season.StartDate,
                    EndDate = season.EndDate,
                    DisplayName = $"{season.SeasonName} {year}",
                    IsActive = season.IsActive,
                    DaysUntilStart = null,
                    DaysIntoSeason = daysIntoSeason >= 0 ? daysIntoSeason : null
                };

                _logger.LogInformation(
                    "Current season: {SeasonName} {Year}, {Days} days into season",
                    season.SeasonName, year, daysIntoSeason);

                return Result<CurrentSeasonInfo>.Success(response);
            }

            return Result<CurrentSeasonInfo>.Failure("No current season found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current season");
            return Result<CurrentSeasonInfo>.Failure($"Error retrieving current season: {ex.Message}");
        }
    }
}

