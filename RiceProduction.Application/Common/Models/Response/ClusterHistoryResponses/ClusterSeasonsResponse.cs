namespace RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;

public class ClusterSeasonsResponse
{
    public CurrentSeasonOption? CurrentSeason { get; set; }
    public List<SeasonOption> PastSeasons { get; set; } = new();
    public List<SeasonOption> UpcomingSeasons { get; set; } = new();
}

public class SeasonOption
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool HasGroups { get; set; }
    public int GroupCount { get; set; }
    public int TotalPlots { get; set; }
    public decimal TotalArea { get; set; }
}

public class CurrentSeasonOption : SeasonOption
{
    public decimal SelectionProgress { get; set; }
    public int SelectionsPending { get; set; }
    public bool CanFormGroups { get; set; }
}

