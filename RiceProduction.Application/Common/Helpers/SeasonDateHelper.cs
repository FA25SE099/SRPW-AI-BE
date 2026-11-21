using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Helpers;

public static class SeasonDateHelper
{
    public static bool TryParseMonthDay(string? dateStr, out int month, out int day)
    {
        month = 0;
        day = 0;

        if (string.IsNullOrWhiteSpace(dateStr))
        {
            return false;
        }

        var parts = dateStr.Split('/');
        return parts.Length == 2
            && int.TryParse(parts[0], out month)
            && int.TryParse(parts[1], out day)
            && month is >= 1 and <= 12
            && day is >= 1 and <= 31;
    }

    public static bool IsDateInSeasonRange(int month, int day, string startDateStr, string endDateStr)
    {
        if (!TryParseMonthDay(startDateStr, out var startMonth, out var startDay) ||
            !TryParseMonthDay(endDateStr, out var endMonth, out var endDay))
        {
            return false;
        }

        var currentDate = month * 100 + day;
        var seasonStart = startMonth * 100 + startDay;
        var seasonEnd = endMonth * 100 + endDay;

        return seasonStart > seasonEnd
            ? currentDate >= seasonStart || currentDate <= seasonEnd
            : currentDate >= seasonStart && currentDate <= seasonEnd;
    }

    public static bool TryGetSeasonDateRange(Season season, int year, out DateTime startDate, out DateTime endDate)
    {
        startDate = default;
        endDate = default;

        if (!TryParseMonthDay(season.StartDate, out var startMonth, out var startDay) ||
            !TryParseMonthDay(season.EndDate, out var endMonth, out var endDay))
        {
            return false;
        }

        try
        {
            startDate = new DateTime(year, startMonth, startDay);
            var endYear = endMonth < startMonth ? year + 1 : year;
            endDate = new DateTime(endYear, endMonth, endDay);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static DateTime ParseSeasonDateToUtc(string dateStr, int year)
    {
        if (!TryParseMonthDay(dateStr, out var month, out var day))
        {
            throw new FormatException($"Invalid season date format: '{dateStr}'. Expected 'MM/DD'.");
        }

        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    public static (Season Season, int Year)? ResolveCurrentSeason(IEnumerable<Season> seasons, DateTime referenceDate)
    {
        var activeSeasons = seasons.Where(s => s.IsActive).ToList();
        if (activeSeasons.Count == 0)
        {
            return null;
        }

        var currentMonth = referenceDate.Month;
        var currentDay = referenceDate.Day;

        foreach (var season in activeSeasons)
        {
            if (!IsDateInSeasonRange(currentMonth, currentDay, season.StartDate, season.EndDate))
            {
                continue;
            }

            var year = referenceDate.Year;
            if (TryParseMonthDay(season.StartDate, out var startMonth, out _) &&
                currentMonth < startMonth &&
                startMonth > 6)
            {
                year--;
            }

            return (season, year);
        }

        return null;
    }
}
