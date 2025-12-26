using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonsByCluster;

public class GetYearSeasonsByClusterQuery : IRequest<Result<YearSeasonsByClusterResponse>>
{
    public Guid ClusterId { get; set; }
    public int? Year { get; set; }
}

public class YearSeasonsByClusterResponse
{
    public Guid ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public YearSeasonDTO? CurrentSeason { get; set; }
    public List<YearSeasonDTO> PastSeasons { get; set; } = new();
    public List<YearSeasonDTO> UpcomingSeasons { get; set; } = new();
    public List<YearSeasonDTO> AllSeasons { get; set; } = new();
}

public class YearSeasonDTO
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string? SeasonType { get; set; }
    public Guid ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public int Year { get; set; }
    public Guid? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? BreakStartDate { get; set; }
    public DateTime? BreakEndDate { get; set; }
    public DateTime? PlanningWindowStart { get; set; }
    public DateTime? PlanningWindowEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? ManagedByExpertId { get; set; }
    public string? ManagedByExpertName { get; set; }
    public int GroupCount { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsPast { get; set; }
    public bool IsUpcoming { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

