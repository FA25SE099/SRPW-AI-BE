using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ReportFeature.Command;

public class ResolveReportCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    public Guid CultivationPlanId { get; set; }

    [Required]
    [MaxLength(100)]
    public string NewVersionName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ResolutionReason { get; set; }

    public Guid? ExpertId { get; set; }

    public Guid? CultivationStageId { get; set; }

    [Required]
    public List<BaseCultivationTaskRequest> BaseCultivationTasks { get; set; } = new();
}

public class BaseCultivationTaskRequest
{
    /// <summary>
    /// The existing CultivationTask ID that this emergency task is based on.
    /// Backend will look up this task's ProductionPlanTaskId for stage information.
    /// After creating the new task, UAV assignments will be updated to reference the new task ID.
    /// </summary>
    public Guid? CultivationPlanTaskId { get; set; }

    [MaxLength(255)]
    public string? TaskName { get; set; }

    public string? Description { get; set; }

    public TaskType? TaskType { get; set; }

    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public TaskStatus? Status { get; set; }

    public int? ExecutionOrder { get; set; }

    public bool IsContingency { get; set; } = true;

    public string? ContingencyReason { get; set; }

    public Guid? DefaultAssignedToUserId { get; set; }

    public Guid? DefaultAssignedToVendorId { get; set; }

    [Required]
    public List<MaterialPerHectareRequest> MaterialsPerHectare { get; set; } = new();
}

public class MaterialPerHectareRequest
{
    [Required]
    public Guid MaterialId { get; set; }

    [Required]
    public decimal QuantityPerHa { get; set; }

    public string? Notes { get; set; }
}

