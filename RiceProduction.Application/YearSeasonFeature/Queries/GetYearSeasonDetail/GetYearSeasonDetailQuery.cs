using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonDetail;

public class GetYearSeasonDetailQuery : IRequest<Result<YearSeasonDetailDTO>>
{
    public Guid Id { get; set; }
}

public class YearSeasonDetailDTO
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string? SeasonType { get; set; }
    public Guid ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public int Year { get; set; }
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
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
    public List<GroupSummaryDTO> Groups { get; set; } = new();
}

public class GroupSummaryDTO
{
    public Guid Id { get; set; }
    public string? GroupName { get; set; }
    public Guid? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? TotalArea { get; set; }
    public int PlotCount { get; set; }
}

