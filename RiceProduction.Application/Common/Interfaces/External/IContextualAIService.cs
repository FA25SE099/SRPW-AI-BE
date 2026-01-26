using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces.External;

/// <summary>
/// Contextual AI recommendations that analyze existing plan and suggest improvements
/// </summary>
public interface IContextualAIService
{
    /// <summary>
    /// Generate contextual suggestions based on report and existing plan
    /// </summary>
    Task<ContextualPlanSuggestions> GenerateContextualSuggestionsAsync(ContextualPlanRequest request);
}

/// <summary>
/// Request for contextual AI suggestions with existing plan context
/// </summary>
public class ContextualPlanRequest
{
    // Emergency Report Context
    public string AlertType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }
    public string? RiceVariety { get; set; }
    public string? CurrentGrowthStage { get; set; }
    public List<string>? DetectedPests { get; set; }
    public double? AiConfidence { get; set; }
    
    // Existing Plan Context
    public string? CurrentVersionName { get; set; }
    public List<ExistingTaskContext> ExistingTasks { get; set; } = new();
    
    // Available Materials from Database
    public List<AvailableMaterialContext> AvailableMaterials { get; set; } = new();
}

/// <summary>
/// Existing task in the current plan
/// </summary>
public class ExistingTaskContext
{
    public int TaskIndex { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public int ExecutionOrder { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public List<ExistingMaterialContext> Materials { get; set; } = new();
}

/// <summary>
/// Existing material in current task
/// </summary>
public class ExistingMaterialContext
{
    public string MaterialName { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// Available material from database
/// </summary>
public class AvailableMaterialContext
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
}

/// <summary>
/// AI-generated contextual suggestions (incremental changes)
/// </summary>
public class ContextualPlanSuggestions
{
    public string OverallAssessment { get; set; } = string.Empty;
    public List<PlanSuggestion> Suggestions { get; set; } = new();
    public List<string> GeneralAdvice { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Individual suggestion that can be applied independently
/// </summary>
public class PlanSuggestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public SuggestionType Type { get; set; }
    public string Priority { get; set; } = string.Empty; // Critical, High, Medium, Low
    public string Description { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public SuggestionAction Action { get; set; } = new();
}

/// <summary>
/// Type of suggestion
/// </summary>
public enum SuggestionType
{
    AddMaterial,
    ModifyMaterialQuantity,
    RemoveMaterial,
    ReplaceMaterial,
    AddTask,
    ModifyTask,
    RemoveTask,
    ReorderTasks,
    ChangeSchedule,
    AddTaskDescription,
    ChangeTaskType
}

/// <summary>
/// Action details for the suggestion
/// </summary>
public class SuggestionAction
{
    // Target identification
    public int? TargetTaskIndex { get; set; }
    public string? TargetTaskName { get; set; }
    public string? TargetMaterialName { get; set; }
    
    // For AddMaterial / ReplaceMaterial
    public Guid? NewMaterialId { get; set; }
    public string? NewMaterialName { get; set; }
    public decimal? NewQuantityPerHa { get; set; }
    public string? NewUnit { get; set; }
    public string? MaterialPurpose { get; set; }
    public List<string>? AlternativeMaterials { get; set; }
    
    // For ModifyMaterialQuantity
    public decimal? CurrentQuantity { get; set; }
    public decimal? RecommendedQuantity { get; set; }
    
    // For AddTask
    public string? NewTaskName { get; set; }
    public string? NewTaskDescription { get; set; }
    public string? NewTaskType { get; set; }
    public int? InsertAfterTaskIndex { get; set; }
    public int? NewExecutionOrder { get; set; }
    public int? DaysFromNow { get; set; }
    public List<NewTaskMaterial>? NewTaskMaterials { get; set; }
    
    // For ModifyTask
    public string? UpdatedTaskName { get; set; }
    public string? UpdatedDescription { get; set; }
    public string? UpdatedTaskType { get; set; }
    
    // For ChangeSchedule
    public DateTime? NewScheduledDate { get; set; }
    public int? DelayByDays { get; set; }
    
    // For ReorderTasks
    public int? NewExecutionOrderValue { get; set; }
}

/// <summary>
/// Material for new task suggestion
/// </summary>
public class NewTaskMaterial
{
    public string MaterialName { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
}

