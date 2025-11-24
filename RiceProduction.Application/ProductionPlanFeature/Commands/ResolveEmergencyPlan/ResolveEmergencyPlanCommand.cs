using RiceProduction.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ProductionPlanFeature.Commands.ResolveEmergencyPlan;

public class ResolveEmergencyPlanCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    [MaxLength(100)]
    public string NewVersionName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ResolutionReason { get; set; }

    public Guid? ExpertId { get; set; }

    /// <summary>
    /// Production Stage ID where the emergency task will be created (if needed)
    /// </summary>
    [Required]
    public Guid ProductionStageId { get; set; }

    /// <summary>
    /// List of plot IDs that need emergency cultivation tasks
    /// Base cultivation tasks will be created for each of these plots
    /// </summary>
    [Required]
    public List<Guid> PlotIds { get; set; } = new();

    /// <summary>
    /// Base cultivation tasks to be created for each plot
    /// These will be replicated for every plot in PlotIds with scaled materials
    /// </summary>
    [Required]
    public List<BaseCultivationTaskRequest> BaseCultivationTasks { get; set; } = new();
}

/// <summary>
/// Base cultivation task template that will be created for each plot
/// </summary>
public class BaseCultivationTaskRequest
{
    /// <summary>
    /// If null, will create new "EmergencySolution" ProductionPlanTask
    /// If provided, will use existing ProductionPlanTask
    /// </summary>
    public Guid? ProductionPlanTaskId { get; set; }

    [MaxLength(255)]
    public string? TaskName { get; set; }

    public string? Description { get; set; }

    public RiceProduction.Domain.Enums.TaskType? TaskType { get; set; }

    public DateTime? ScheduledEndDate { get; set; }

    public RiceProduction.Domain.Enums.TaskStatus? Status { get; set; }

    public int? ExecutionOrder { get; set; }

    public bool IsContingency { get; set; } = true;

    public string? ContingencyReason { get; set; }

    /// <summary>
    /// Optional: Default assignment for supervisor (can be overridden per plot)
    /// </summary>
    public Guid? DefaultAssignedToUserId { get; set; }

    /// <summary>
    /// Optional: Default assignment for UAV vendor (can be overridden per plot)
    /// </summary>
    public Guid? DefaultAssignedToVendorId { get; set; }

    /// <summary>
    /// Materials for this task (per 1 hectare standard)
    /// Will be scaled by each plot's area
    /// </summary>
    [Required]
    public List<TaskMaterialRequest> MaterialsPerHectare { get; set; } = new();
}

/// <summary>
/// Material requirement per hectare for a base cultivation task
/// </summary>
public class TaskMaterialRequest
{
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Quantity per hectare (will be scaled by plot area)
    /// </summary>
    [Required]
    public decimal QuantityPerHa { get; set; }

    public string? Notes { get; set; }
}