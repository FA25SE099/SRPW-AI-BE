using MediatR;
using RiceProduction.Application.Common.Models;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonDashboard;

public class GetYearSeasonDashboardQuery : IRequest<Result<YearSeasonDashboardDto>>
{
    [Required]
    public Guid YearSeasonId { get; set; }
}

public class YearSeasonDashboardDto
{
    // Season Information
    public YearSeasonInfo Season { get; set; } = new();
    
    // Group Formation Status
    public GroupFormationStatus GroupStatus { get; set; } = new();
    
    // Production Planning Status
    public ProductionPlanningStatus PlanningStatus { get; set; } = new();
    
    // Material Distribution Status
    public MaterialDistributionStatus MaterialStatus { get; set; } = new();
    
    // Timeline
    public YearSeasonTimeline Timeline { get; set; } = new();
    
    // Alerts
    public List<YearSeasonAlert> Alerts { get; set; } = new();
}

public class YearSeasonInfo
{
    public Guid YearSeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string SeasonType { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public string ClusterName { get; set; } = string.Empty;
    public string? ExpertName { get; set; }
    public int? AllowedPlantingFlexibilityDays { get; set; }
    public int? MaterialConfirmationDaysBeforePlanting { get; set; }
}

public class GroupFormationStatus
{
    public int TotalGroups { get; set; }
    public int ActiveGroups { get; set; }
    public int DraftGroups { get; set; }
    public int CompletedGroups { get; set; }
    public int GroupsWithSupervisor { get; set; }
    public int GroupsWithoutSupervisor { get; set; }
    public decimal TotalAreaHectares { get; set; }
    public int TotalPlotsInGroups { get; set; }
    public int TotalFarmersInGroups { get; set; }
}

public class ProductionPlanningStatus
{
    public int TotalPlans { get; set; }
    public int PlansDraft { get; set; }
    public int PlansPendingApproval { get; set; }
    public int PlansApproved { get; set; }
    public int PlansCancelled { get; set; }
    public int GroupsWithPlans { get; set; }
    public int GroupsWithoutPlans { get; set; }
    public decimal PlanningCompletionRate { get; set; }
    public DateTime? EarliestPlanSubmission { get; set; }
    public DateTime? LatestPlanApproval { get; set; }
}

public class MaterialDistributionStatus
{
    public int TotalDistributions { get; set; }
    public int DistributionsPending { get; set; }
    public int DistributionsPartiallyConfirmed { get; set; }
    public int DistributionsCompleted { get; set; }
    public int DistributionsRejected { get; set; }
    public int DistributionsOverdue { get; set; }
    public decimal MaterialCompletionRate { get; set; }
    public int UniqueMaterialsDistributed { get; set; }
    public int PlotsReceivingMaterials { get; set; }
}

public class YearSeasonTimeline
{
    public DateTime? PlanningWindowStart { get; set; }
    public DateTime? PlanningWindowEnd { get; set; }
    public DateTime SeasonStartDate { get; set; }
    public DateTime SeasonEndDate { get; set; }
    public DateTime? BreakStartDate { get; set; }
    public DateTime? BreakEndDate { get; set; }
    
    // Calculated fields
    public int DaysUntilPlanningWindowStart { get; set; }
    public int DaysUntilPlanningWindowEnd { get; set; }
    public int DaysUntilSeasonStart { get; set; }
    public int DaysUntilSeasonEnd { get; set; }
    public int TotalSeasonDays { get; set; }
    public int DaysElapsed { get; set; }
    public int DaysRemaining { get; set; }
    public decimal ProgressPercentage { get; set; }
    
    // Status flags
    public bool IsPlanningWindowOpen { get; set; }
    public bool HasSeasonStarted { get; set; }
    public bool HasSeasonEnded { get; set; }
}

public class YearSeasonAlert
{
    public string Type { get; set; } = string.Empty; // "Error", "Warning", "Info", "Success"
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "Planning", "Groups", "Materials", "Timeline"
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

