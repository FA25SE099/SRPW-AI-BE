using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.StandardPlanFeature.Queries.GetStandardPlanDetail;

/// <summary>
/// Complete detail response for a Standard Plan
/// </summary>
public class StandardPlanDetailDto
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TotalDurationDays { get; set; }
    public bool IsActive { get; set; }
    
    // Rice Variety Information
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    
    // Creator Information
    public Guid? CreatedBy { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    
    // Hierarchical Data
    public List<StandardPlanStageDetailDto> Stages { get; set; } = new();
    
    // Summary Statistics
    public int TotalStages { get; set; }
    public int TotalTasks { get; set; }
    public int TotalMaterialTypes { get; set; }
}

/// <summary>
/// Detail for a Standard Plan Stage
/// </summary>
public class StandardPlanStageDetailDto
{
    public Guid Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public int? ExpectedDurationDays { get; set; }
    public bool IsMandatory { get; set; }
    public string? Notes { get; set; }
    
    public List<StandardPlanTaskDetailDto> Tasks { get; set; } = new();
}

/// <summary>
/// Detail for a Standard Plan Task
/// </summary>
public class StandardPlanTaskDetailDto
{
    public Guid Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType TaskType { get; set; }
    public TaskPriority Priority { get; set; }
    public int DaysAfter { get; set; }
    public int DurationDays { get; set; }
    public int SequenceOrder { get; set; }
    
    public List<StandardPlanTaskMaterialDetailDto> Materials { get; set; } = new();
}

/// <summary>
/// Detail for materials required for a Standard Plan Task
/// </summary>
public class StandardPlanTaskMaterialDetailDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    public string MaterialUnit { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
}

