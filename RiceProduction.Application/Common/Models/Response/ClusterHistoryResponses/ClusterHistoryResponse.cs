namespace RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;

public class ClusterHistoryResponse
{
    public Guid ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public List<ClusterSeasonSnapshot> SeasonSnapshots { get; set; } = new();
}

public class ClusterSeasonSnapshot
{
    // Season Info
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string SeasonType { get; set; } = string.Empty;
    public int Year { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    
    // Cluster metrics for this season
    public int TotalGroups { get; set; }
    public int TotalPlots { get; set; }
    public int TotalFarmers { get; set; }
    public decimal TotalArea { get; set; }
    
    // Group breakdown by rice variety
    public List<RiceVarietyGroupSummary> RiceVarietyBreakdown { get; set; } = new();
    
    // Performance metrics
    public decimal? AverageYield { get; set; }
    public decimal? TotalProduction { get; set; }
    public int CompletedProductionPlans { get; set; }
    
    // Groups in this season
    public List<GroupSeasonSummary> Groups { get; set; } = new();
}

public class RiceVarietyGroupSummary
{
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public int GroupCount { get; set; }
    public int PlotCount { get; set; }
    public decimal TotalArea { get; set; }
}

public class GroupSeasonSummary
{
    public Guid GroupId { get; set; }
    public Guid? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }
    public Guid? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime? PlantingDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PlotCount { get; set; }
    public decimal? TotalArea { get; set; }
}

