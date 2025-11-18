using System.ComponentModel.DataAnnotations;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Request;

public class StandardPlanTaskRequest
{
    [Required]
    [MaxLength(255)]
    public string TaskName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Number of days after base planting date (can be negative for pre-planting tasks)
    /// </summary>
    [Required]
    public int DaysAfter { get; set; }

    /// <summary>
    /// How many days this task takes to complete
    /// </summary>
    public int DurationDays { get; set; } = 1;

    [Required]
    public TaskType TaskType { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    [Required]
    public int SequenceOrder { get; set; }

    /// <summary>
    /// List of materials required for this task.
    /// </summary>
    public List<StandardPlanTaskMaterialRequest> Materials { get; set; } = new();
}

