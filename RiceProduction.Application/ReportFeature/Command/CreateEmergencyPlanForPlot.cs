using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ReportFeature.Command;

public class CreateEmergencyPlanForPlotCommand : IRequest<Result<Guid>>
{
    /// <summary>
    /// The emergency report ID that triggered this emergency plan
    /// </summary>
    [Required]
    public Guid EmergencyReportId { get; set; }

    /// <summary>
    /// The specific plot that needs emergency treatment
    /// </summary>
    [Required]
    public Guid PlotId { get; set; }

    /// <summary>
    /// The production plan this emergency relates to
    /// </summary>
    [Required]
    public Guid ProductionPlanId { get; set; }

    /// <summary>
    /// Production Stage ID where the emergency task will be created (optional - will auto-create if not provided)
    /// </summary>
    public Guid? ProductionStageId { get; set; }

    /// <summary>
    /// Name for the new cultivation version
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string NewVersionName { get; set; } = string.Empty;

    /// <summary>
    /// Reason for creating this emergency version
    /// </summary>
    [MaxLength(500)]
    public string? ResolutionReason { get; set; }

    /// <summary>
    /// Expert ID who is resolving this emergency (will be set from auth context)
    /// </summary>
    public Guid? ExpertId { get; set; }

    /// <summary>
    /// Emergency cultivation tasks to be created for this single plot
    /// </summary>
    [Required]
    public List<EmergencyPlotTaskRequest> EmergencyTasks { get; set; } = new();
}

/// <summary>
/// Emergency task template for a single plot
/// </summary>
public class EmergencyPlotTaskRequest
{
    /// <summary>
    /// If provided, will use existing ProductionPlanTask; otherwise creates new one
    /// </summary>
    public Guid? ProductionPlanTaskId { get; set; }

    [MaxLength(255)]
    public string? TaskName { get; set; }

    public string? Description { get; set; }

    public TaskType? TaskType { get; set; }

    public DateTime? ScheduledEndDate { get; set; }

    public TaskStatus? Status { get; set; }

    public int? ExecutionOrder { get; set; }

    public bool IsContingency { get; set; } = true;

    public string? ContingencyReason { get; set; }

    /// <summary>
    /// Optional: Assign to specific user (supervisor)
    /// </summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>
    /// Optional: Assign to specific vendor (UAV service)
    /// </summary>
    public Guid? AssignedToVendorId { get; set; }

    /// <summary>
    /// Materials for this emergency task (absolute quantities, NOT per hectare)
    /// These will be applied directly without scaling
    /// </summary>
    [Required]
    public List<EmergencyTaskMaterialRequest> Materials { get; set; } = new();
}

/// <summary>
/// Material requirement for an emergency task (absolute quantity)
/// </summary>
public class EmergencyTaskMaterialRequest
{
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Absolute quantity of material (NOT per hectare)
    /// </summary>
    [Required]
    public decimal Quantity { get; set; }

    public string? Notes { get; set; }
}































