using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Queries.GetCurrentSeason;

public class GetCurrentSeasonQuery : IRequest<Result<CurrentSeasonInfo>>
{
}

public class CurrentSeasonInfo
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string SeasonType { get; set; } = string.Empty;
    public int Year { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? DaysUntilStart { get; set; }
    public int? DaysIntoSeason { get; set; }
}

