using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.Common.Models.Request.MaterialCostCalculationRequests;

/// <summary>
/// Input model for a task with its materials (shared across all material cost calculation queries)
/// </summary>
public class TaskWithMaterialsInput
{
    [Required]
    public string TaskName { get; set; } = string.Empty;

    public string? TaskDescription { get; set; }

    [Required]
    public List<TaskMaterialInput> Materials { get; set; } = new();
}

/// <summary>
/// Input model for a material within a task
/// </summary>
public class TaskMaterialInput
{
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Quantity required per hectare
    /// </summary>
    [Required]
    public decimal QuantityPerHa { get; set; }
}