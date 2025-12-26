using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonReadiness;

public class GetYearSeasonReadinessQuery : IRequest<Result<YearSeasonReadinessDto>>
{
    public Guid YearSeasonId { get; set; }
}

public class YearSeasonReadinessDto
{
    public Guid YearSeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public bool HasGroups { get; set; }
    public ClusterReadinessInfo Readiness { get; set; } = new();
}

public class ClusterReadinessInfo
{
    public bool IsReadyToFormGroups { get; set; }
    public int AvailablePlots { get; set; }
    public int PlotsWithPolygon { get; set; }
    public int PlotsWithoutPolygon { get; set; }
    public int AvailableSupervisors { get; set; }
    public int AvailableFarmers { get; set; }
    public int FarmersWithSelection { get; set; }
    public int ReadinessScore { get; set; }
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

