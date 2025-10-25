using System.ComponentModel.DataAnnotations;
using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.Common.Models.Request;

public class ProductionPlanTaskRequest
{
    [Required]
    [MaxLength(255)]
    public string TaskName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public TaskType TaskType { get; set; } // Assuming TaskType enum exists

    [Required]
    public DateTime ScheduledDate { get; set; }

    public DateTime? ScheduledEndDate { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Normal; // Assuming TaskPriority enum exists

    [Required]
    public int SequenceOrder { get; set; }

    /// <summary>
    /// List of materials required for this task.
    /// </summary>
    public List<ProductionPlanTaskMaterialRequest> Materials { get; set; } = new();
}