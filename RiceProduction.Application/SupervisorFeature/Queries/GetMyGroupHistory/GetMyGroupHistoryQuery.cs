using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetMyGroupHistory;

public class GetMyGroupHistoryQuery : IRequest<Result<List<GroupHistorySummary>>>
{
    public Guid SupervisorId { get; set; }
    public int? Year { get; set; }
    public bool IncludeCurrentSeason { get; set; } = false;
}

public class GroupHistorySummary
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public HistorySeasonInfo Season { get; set; } = new();
    public decimal? TotalArea { get; set; }
    public int TotalPlots { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime? PlantingDate { get; set; }
    public int ProductionPlansCount { get; set; }
    public string? ClusterName { get; set; }
}

public class HistorySeasonInfo
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string SeasonType { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

