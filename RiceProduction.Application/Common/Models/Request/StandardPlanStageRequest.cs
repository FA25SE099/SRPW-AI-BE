using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.Common.Models.Request;

public class StandardPlanStageRequest
{
    [Required]
    [MaxLength(100)]
    public string StageName { get; set; } = string.Empty;

    [Required]
    public int SequenceOrder { get; set; }

    public int? ExpectedDurationDays { get; set; }

    public bool IsMandatory { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Tasks belonging to this stage.
    /// </summary>
    public List<StandardPlanTaskRequest> Tasks { get; set; } = new();
}

