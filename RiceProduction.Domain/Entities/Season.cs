namespace RiceProduction.Domain.Entities;

public class Season : BaseAuditableEntity
{
    [Required]
    [MaxLength(100)]
    public string SeasonName { get; set; } = string.Empty;

    [Required]
    public string StartDate { get; set; } = string.Empty; // Format "MM/DD"

    [Required]
    public string EndDate { get; set; } = string.Empty; // Format "MM/DD"

    /// <summary>
    /// Type of season (e.g., "Wet Season", "Dry Season", "Winter-Spring")
    /// </summary>
    [MaxLength(50)]
    public string? SeasonType { get; set; }

    /// <summary>
    /// Whether this season is currently active for planning
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<RiceVarietySeason> RiceVarietySeasons { get; set; } = new List<RiceVarietySeason>();
    public ICollection<PlotCultivation> PlotCultivations { get; set; } = new List<PlotCultivation>();
    public ICollection<YearSeason> YearSeasons { get; set; } = new List<YearSeason>();

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

    public static bool IsDateInRange(int month, int day, string startDateStr, string endDateStr)
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

    public bool ContainsDate(int month, int day) =>
        IsDateInRange(month, day, StartDate, EndDate);

    public bool ContainsDate(DateTime date) =>
        ContainsDate(date.Month, date.Day);

    public bool TryGetDateRange(int year, out DateTime startDate, out DateTime endDate)
    {
        startDate = default;
        endDate = default;

        if (!TryParseMonthDay(StartDate, out var startMonth, out var startDay) ||
            !TryParseMonthDay(EndDate, out var endMonth, out var endDay))
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

    public int ResolveYearForDate(DateTime referenceDate)
    {
        var year = referenceDate.Year;
        if (TryParseMonthDay(StartDate, out var startMonth, out _) &&
            referenceDate.Month < startMonth &&
            startMonth > 6)
        {
            year--;
        }

        return year;
    }
}
