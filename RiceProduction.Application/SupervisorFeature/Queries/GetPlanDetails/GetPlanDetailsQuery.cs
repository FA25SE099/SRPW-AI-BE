using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetPlanDetails;

public class GetPlanDetailsQuery : IRequest<Result<PlanDetailsResponse>>
{
    public Guid SupervisorId { get; set; }
    public Guid ProductionPlanId { get; set; }
}

/// <summary>
/// Detailed production plan response with full stage/task breakdown
/// </summary>
public class PlanDetailsResponse
{
    // Basic Plan Info
    public Guid ProductionPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime BasePlantingDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public decimal? TotalArea { get; set; }
    
    // Group Context
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public int Year { get; set; }
    
    // Overall Progress
    public int TotalStages { get; set; }
    public int CompletedStages { get; set; }
    public int InProgressStages { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    public decimal OverallProgressPercentage { get; set; }
    
    // Time Tracking
    public int DaysElapsed { get; set; }
    public int EstimatedTotalDays { get; set; }
    public bool IsOnSchedule { get; set; }
    public int? DaysBehindSchedule { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    
    // Cost Tracking
    public decimal EstimatedTotalCost { get; set; }
    public decimal ActualCostToDate { get; set; }
    public decimal RemainingEstimatedCost { get; set; }
    public decimal CostVariance { get; set; }
    public decimal CostVariancePercentage { get; set; }
    
    // Contingency Summary
    public int ContingencyTasksCount { get; set; }
    public int TasksWithInterruptions { get; set; }
    public bool HasActiveIssues { get; set; }
    
    // Detailed Stage Breakdown
    public List<StageDetails> Stages { get; set; } = new();
    
    // Plot-Level Progress
    public List<PlotProgressDetails> PlotProgress { get; set; } = new();
}

/// <summary>
/// Detailed stage information with tasks
/// </summary>
public class StageDetails
{
    public Guid StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int StageOrder { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
    
    // Progress
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    public int ContingencyTasks { get; set; }
    public decimal ProgressPercentage { get; set; }
    public string Status { get; set; } = string.Empty; // "Not Started", "In Progress", "Completed", "Delayed"
    
    // Timing
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public bool IsDelayed { get; set; }
    public int? DaysDelayed { get; set; }
    
    // Cost
    public decimal EstimatedStageCost { get; set; }
    public decimal ActualStageCost { get; set; }
    public decimal CostVariance { get; set; }
    
    // Tasks in this stage
    public List<TaskDetails> Tasks { get; set; } = new();
}

/// <summary>
/// Detailed task information
/// </summary>
public class TaskDetails
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    
    // Status
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    
    // Timing
    public DateTime ScheduledDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? DaysDelayed { get; set; }
    
    // Contingency
    public bool IsContingency { get; set; }
    public string? ContingencyReason { get; set; }
    public string? InterruptionReason { get; set; }
    public string? WeatherConditions { get; set; }
    
    // Cost
    public decimal EstimatedCost { get; set; }
    public decimal ActualMaterialCost { get; set; }
    public decimal ActualServiceCost { get; set; }
    public decimal TotalActualCost { get; set; }
    
    // Assignment
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public Guid? AssignedToVendorId { get; set; }
    public string? VendorName { get; set; }
    
    // Verification
    public Guid? VerifiedBy { get; set; }
    public string? VerifierName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    
    // Materials
    public List<TaskMaterial> Materials { get; set; } = new();
    
    // Execution count across plots
    public int TotalExecutions { get; set; }
    public int CompletedExecutions { get; set; }
    public int InProgressExecutions { get; set; }
}

/// <summary>
/// Material information for a task
/// </summary>
public class TaskMaterial
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal EstimatedAmount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>
/// Plot-level progress details
/// </summary>
public class PlotProgressDetails
{
    public Guid PlotId { get; set; }
    public string PlotIdentifier { get; set; } = string.Empty; // "SoThua 1, SoTo 2"
    public decimal Area { get; set; }
    public string? SoilType { get; set; }
    
    // Farmer
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? FarmerPhone { get; set; }
    
    // Progress
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    public decimal ProgressPercentage { get; set; }
    
    // Cost
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal CostVariance { get; set; }
    
    // Issues
    public int ContingencyCount { get; set; }
    public bool HasActiveIssues { get; set; }
    
    // Latest activity
    public string? LatestCompletedTask { get; set; }
    public DateTime? LatestCompletedAt { get; set; }
    public string? NextScheduledTask { get; set; }
    public DateTime? NextScheduledDate { get; set; }
    
    // Current stage
    public string? CurrentStageName { get; set; }
    public int? CurrentStageOrder { get; set; }
}

