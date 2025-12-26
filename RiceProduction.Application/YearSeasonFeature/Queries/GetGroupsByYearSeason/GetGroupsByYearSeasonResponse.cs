using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetGroupsByYearSeason;

/// <summary>
/// Response containing groups for a YearSeason
/// </summary>
public class GetGroupsByYearSeasonResponse
{
    public Guid YearSeasonId { get; set; }
    public string YearSeasonDisplayName { get; set; } = string.Empty;
    public Guid ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public Guid? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public int TotalGroupCount { get; set; }
    public List<YearSeasonGroupDTO> Groups { get; set; } = new List<YearSeasonGroupDTO>();
    public GroupStatusSummary StatusSummary { get; set; } = new GroupStatusSummary();
}

/// <summary>
/// DTO for a group in the context of a YearSeason
/// </summary>
public class YearSeasonGroupDTO
{
    public Guid GroupId { get; set; }
    public string? GroupName { get; set; }
    public Guid ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public Guid? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }
    public Guid? YearSeasonId { get; set; }
    public int Year { get; set; }
    public DateTime? PlantingDate { get; set; }
    public GroupStatus Status { get; set; }
    public bool IsException { get; set; }
    public string? ExceptionReason { get; set; }
    public DateTime? ReadyForUavDate { get; set; }
    public string? Area { get; set; }
    public decimal? TotalArea { get; set; }
    
    // Additional information
    public int PlotCount { get; set; }
    public int FarmerCount { get; set; }
    public int ProductionPlanCount { get; set; }
    public int UavServiceOrderCount { get; set; }
    public int AlertCount { get; set; }
}

/// <summary>
/// Summary of groups by status
/// </summary>
public class GroupStatusSummary
{
    public int DraftCount { get; set; }
    public int ActiveCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int TotalCount { get; set; }
}

