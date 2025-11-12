using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.ViewGroupBySeason;

public class ViewGroupBySeasonQuery : IRequest<Result<GroupBySeasonResponse>>
{
    public Guid SupervisorId { get; set; }
    public Guid? SeasonId { get; set; }  // null = current season
    public int? Year { get; set; }        // null = current year
}

/// <summary>
/// Group state in its lifecycle
/// </summary>
public enum GroupState
{
    PrePlanning,      // No plan yet, ready to create one
    Planning,         // Plan created but not approved
    InProduction,     // Plan approved and tasks in progress
    Completed,        // All tasks completed this season
    Archived          // Past season, historical data
}

/// <summary>
/// Lightweight overview response - fast loading
/// </summary>
public class GroupBySeasonResponse
{
    // Basic Group Info
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    // Season Context
    public GroupSeasonInfo Season { get; set; } = new();
    public bool IsCurrentSeason { get; set; }
    public bool IsPastSeason { get; set; }
    public GroupState CurrentState { get; set; }
    
    // Group Details
    public decimal? TotalArea { get; set; }
    public string? AreaGeoJson { get; set; }
    public DateTime? PlantingDate { get; set; }
    public Guid? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public Guid ClusterId { get; set; }
    public string? ClusterName { get; set; }
    
    // Plots (always shown with optional readiness)
    public List<PlotOverview> Plots { get; set; } = new();
    
    // Readiness - ONLY for PrePlanning state
    public GroupReadinessOverview? Readiness { get; set; }
    
    // Plan Progress - ONLY summary for active/completed plans
    public ProductionPlanOverview? PlanOverview { get; set; }
    
    // Economics - ONLY summary for completed/archived
    public EconomicOverview? Economics { get; set; }
}

public class GroupSeasonInfo
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string SeasonType { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Year { get; set; }
}

/// <summary>
/// Plot with optional readiness (lightweight)
/// </summary>
public class PlotOverview
{
    public Guid PlotId { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public decimal Area { get; set; }
    public string? SoilType { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // Polygon
    public bool HasPolygon { get; set; }
    public string? PolygonGeoJson { get; set; }
    public string? CentroidGeoJson { get; set; }
    
    // Farmer
    public Guid FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public string? FarmerPhone { get; set; }
    public string? FarmerAddress { get; set; }
    public string? FarmCode { get; set; }
    
    // Readiness (only in PrePlanning state)
    public PlotReadinessInfo? Readiness { get; set; }
}

/// <summary>
/// Individual plot readiness check
/// </summary>
public class PlotReadinessInfo
{
    public bool IsReady { get; set; }
    public string ReadinessLevel { get; set; } = string.Empty; // "Ready", "Warning", "Blocked"
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Group-level readiness overview
/// </summary>
public class GroupReadinessOverview
{
    public bool IsReady { get; set; }
    public int ReadinessScore { get; set; } // 0-100
    public string ReadinessLevel { get; set; } = string.Empty; // "Ready", "Almost Ready", "In Progress", "Not Ready"
    
    // Summary counts
    public int TotalPlots { get; set; }
    public int ReadyPlots { get; set; }
    public int PlotsWithIssues { get; set; }
    public int PlotsWithPolygon { get; set; }
    public int PlotsWithoutPolygon { get; set; }
    
    // Group-level checks
    public bool HasRiceVariety { get; set; }
    public bool HasTotalArea { get; set; }
    public bool HasPlots { get; set; }
    public bool AllPlotsReady { get; set; }
    
    // Issues
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Production plan progress summary (lightweight - no task details)
/// </summary>
public class ProductionPlanOverview
{
    public Guid ProductionPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime BasePlantingDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    // High-level progress
    public int TotalStages { get; set; }
    public int CompletedStages { get; set; }
    public int InProgressStages { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public decimal OverallProgressPercentage { get; set; }
    
    // Time tracking
    public int DaysElapsed { get; set; }
    public int EstimatedTotalDays { get; set; }
    public bool IsOnSchedule { get; set; }
    public int? DaysBehindSchedule { get; set; }
    
    // Cost summary
    public decimal EstimatedTotalCost { get; set; }
    public decimal ActualCostToDate { get; set; }
    public decimal CostVariancePercentage { get; set; }
    
    // Summary stats
    public int ContingencyTasksCount { get; set; }
    public bool HasActiveIssues { get; set; }
    
    // Link to detailed view
    public bool HasDetailedProgress { get; set; } = true; // User can click to view details
}

/// <summary>
/// Economic summary (lightweight - no plot breakdown)
/// </summary>
public class EconomicOverview
{
    // Cost Summary
    public decimal TotalEstimatedCost { get; set; }
    public decimal TotalActualCost { get; set; }
    public decimal CostVariance { get; set; }
    public decimal CostVariancePercentage { get; set; }
    
    // Cost Breakdown
    public decimal ActualMaterialCost { get; set; }
    public decimal ActualServiceCost { get; set; }
    
    // Yield Summary
    public decimal ExpectedYield { get; set; }
    public decimal ActualYield { get; set; }
    public decimal YieldVariance { get; set; }
    public decimal YieldVariancePercentage { get; set; }
    public decimal YieldPerHectare { get; set; }
    
    // Financial Performance
    public decimal GrossProfit { get; set; }
    public decimal ProfitMargin { get; set; }
    public decimal ReturnOnInvestment { get; set; }
    
    // Efficiency
    public decimal CostPerKg { get; set; }
    public decimal CostPerHectare { get; set; }
    
    // Performance Rating
    public string PerformanceRating { get; set; } = string.Empty; // "Excellent", "Good", "Average", "Below Average"
    public int PerformanceScore { get; set; } // 0-100
    
    // Link to detailed view
    public bool HasDetailedEconomics { get; set; } = true; // User can click to view plot breakdown
}

