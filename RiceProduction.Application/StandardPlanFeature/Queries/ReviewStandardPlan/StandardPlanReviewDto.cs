using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.StandardPlanFeature.Queries.ReviewStandardPlan;

/// <summary>
/// Complete preview of a standard plan with calculated dates and quantities
/// </summary>
public class StandardPlanReviewDto
{
    // Plan Information
    public Guid StandardPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Rice Variety Information
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    
    // Input Parameters
    public DateTime SowDate { get; set; }
    public decimal AreaInHectares { get; set; }
    
    // Calculated Timeline
    public DateTime EstimatedStartDate { get; set; }
    public DateTime EstimatedEndDate { get; set; }
    public int TotalDurationDays { get; set; }
    
    // Cost Summary
    public decimal EstimatedTotalCost { get; set; }
    public decimal EstimatedCostPerHectare { get; set; }
    
    // Hierarchical Data with Calculated Values
    public List<StandardPlanStageReviewDto> Stages { get; set; } = new();
    
    // Statistics
    public int TotalStages { get; set; }
    public int TotalTasks { get; set; }
    public int TotalMaterialTypes { get; set; }
    public decimal TotalMaterialQuantity { get; set; }
}

/// <summary>
/// Stage information with calculated dates
/// </summary>
public class StandardPlanStageReviewDto
{
    public Guid StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public int? ExpectedDurationDays { get; set; }
    public string? Notes { get; set; }
    
    // Calculated Dates
    public DateTime? EstimatedStartDate { get; set; }
    public DateTime? EstimatedEndDate { get; set; }
    
    public List<StandardPlanTaskReviewDto> Tasks { get; set; } = new();
}

/// <summary>
/// Task information with calculated schedule and costs
/// </summary>
public class StandardPlanTaskReviewDto
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType TaskType { get; set; }
    public TaskPriority Priority { get; set; }
    public int SequenceOrder { get; set; }
    
    // Timing Information
    public int DaysAfterSowing { get; set; }
    public int DurationDays { get; set; }
    public DateTime ScheduledStartDate { get; set; }
    public DateTime ScheduledEndDate { get; set; }
    
    // Cost Information
    public decimal EstimatedTaskCost { get; set; }
    public decimal EstimatedTaskCostPerHa { get; set; }
    
    public List<StandardPlanTaskMaterialReviewDto> Materials { get; set; } = new();
}

/// <summary>
/// Material requirements with calculated quantities and costs
/// </summary>
public class StandardPlanTaskMaterialReviewDto
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    public string MaterialUnit { get; set; } = string.Empty;
    
    // Quantities
    public decimal QuantityPerHa { get; set; }
    public decimal TotalQuantityNeeded { get; set; }
    
    // Pricing (if available)
    public decimal? UnitPrice { get; set; }
    public decimal? TotalCost { get; set; }
    public DateTime? PriceDate { get; set; } // When the price is valid from
}

