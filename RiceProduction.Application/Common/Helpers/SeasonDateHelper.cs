using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Helpers;

public static class SeasonDateHelper
{
    public static bool TryParseMonthDay(string? dateStr, out int month, out int day) =>
        Season.TryParseMonthDay(dateStr, out month, out day);

    public static bool IsDateInSeasonRange(int month, int day, string startDateStr, string endDateStr) =>
        Season.IsDateInRange(month, day, startDateStr, endDateStr);

    public static bool TryGetSeasonDateRange(Season season, int year, out DateTime startDate, out DateTime endDate) =>
        season.TryGetDateRange(year, out startDate, out endDate);

    public static DateTime ParseSeasonDateToUtc(string dateStr, int year)
    {
        if (!Season.TryParseMonthDay(dateStr, out var month, out var day))
        {
            throw new FormatException($"Invalid season date format: '{dateStr}'. Expected 'MM/DD'.");
        }

        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    public static (Season Season, int Year)? ResolveCurrentSeason(IEnumerable<Season> seasons, DateTime referenceDate)
    {
        foreach (var season in seasons.Where(s => s.IsActive))
        {
            if (season.ContainsDate(referenceDate))
            {
                return (season, season.ResolveYearForDate(referenceDate));
            }
        }

        return null;
    }
}
