using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SeasonFeature.Queries.GetCurrentSeason;

public class GetCurrentSeasonQueryHandler
    : IRequestHandler<GetCurrentSeasonQuery, Result<CurrentSeasonInfo>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetCurrentSeasonQueryHandler> _logger;

    public GetCurrentSeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetCurrentSeasonQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CurrentSeasonInfo>> Handle(
        GetCurrentSeasonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.Now;
            var currentMonth = today.Month;
            var currentDay = today.Day;

            var allSeasons = await _unitOfWork.Repository<Season>()
                .ListAsync(_ => true);

            foreach (var season in allSeasons)
            {
                if (IsDateInSeasonRange(currentMonth, currentDay, season.StartDate, season.EndDate))
                {
                    var startParts = season.StartDate.Split('/');
                    int startMonth = int.Parse(startParts[0]);

                    int year = today.Year;
                    if (currentMonth < startMonth && startMonth > 6)
                    {
                        year--;
                    }

                    // Calculate days into season
                    var seasonStartDate = new DateTime(year, startMonth, int.Parse(startParts[1]));
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
            }

            return Result<CurrentSeasonInfo>.Failure("No current season found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current season");
            return Result<CurrentSeasonInfo>.Failure($"Error retrieving current season: {ex.Message}");
        }
    }

    private bool IsDateInSeasonRange(int month, int day, string startDateStr, string endDateStr)
    {
        try
        {
            var startParts = startDateStr.Split('/');
            var endParts = endDateStr.Split('/');

            int startMonth = int.Parse(startParts[0]);
            int startDay = int.Parse(startParts[1]);
            int endMonth = int.Parse(endParts[0]);
            int endDay = int.Parse(endParts[1]);

            int currentDate = month * 100 + day;
            int seasonStart = startMonth * 100 + startDay;
            int seasonEnd = endMonth * 100 + endDay;

            if (seasonStart > seasonEnd)
            {
                // Season spans across year boundary
                return currentDate >= seasonStart || currentDate <= seasonEnd;
            }
            else
            {
                return currentDate >= seasonStart && currentDate <= seasonEnd;
            }
        }
        catch
        {
            return false;
        }
    }
}

