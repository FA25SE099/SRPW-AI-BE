using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;

public class ClusterCurrentSeasonResponse
{
    public Guid ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    
    public CurrentSeasonInfo CurrentSeason { get; set; } = new();
    
    public bool HasGroups { get; set; }
    
    // If hasGroups = true
    public List<GroupSeasonSummary>? Groups { get; set; }
    public List<RiceVarietyGroupSummary>? RiceVarietyBreakdown { get; set; }
    public int TotalPlots { get; set; }
    public int TotalFarmers { get; set; }
    public decimal TotalArea { get; set; }
    
    // If hasGroups = false
    public ClusterReadinessInfo? Readiness { get; set; }
    
    // Rice variety selection status (always included)
    public RiceVarietySelectionStatus? RiceVarietySelection { get; set; }
}

public class CurrentSeasonInfo
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string SeasonType { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool IsCurrent { get; set; } = true;
}

public class ClusterReadinessInfo
{
    public bool IsReadyToFormGroups { get; set; }
    public int AvailablePlots { get; set; }
    public int PlotsWithPolygon { get; set; }
    public int PlotsWithoutPolygon { get; set; }
    public int AvailableSupervisors { get; set; }
    public int AvailableFarmers { get; set; }
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public int ReadinessScore { get; set; }
}

public class RiceVarietySelectionStatus
{
    public int TotalFarmers { get; set; }
    public int FarmersWithSelection { get; set; }
    public int FarmersPending { get; set; }
    public decimal SelectionCompletionRate { get; set; }
    
    public List<VarietySelectionSummary> Selections { get; set; } = new();
    public List<PendingFarmerInfo> PendingFarmers { get; set; } = new();
}

public class VarietySelectionSummary
{
    public Guid VarietyId { get; set; }
    public string VarietyName { get; set; } = string.Empty;
    public int SelectedBy { get; set; }
    public int PreviousSeason { get; set; }
    public bool IsRecommended { get; set; }
    public int NewSelections { get; set; }
    public int SwitchedIn { get; set; }
    public int SwitchedOut { get; set; }
}

public class PendingFarmerInfo
{
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? PreviousVariety { get; set; }
    public int PlotCount { get; set; }
}

