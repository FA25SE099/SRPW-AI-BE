using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.Common.Models.Request;
public class ProductionStageRequest
{
    [Required]
    [MaxLength(100)]
    public string StageName { get; set; } = string.Empty;
    
    [Required]
    public int SequenceOrder { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
    
    public int? TypicalDurationDays { get; set; }

    [MaxLength(7)]
    public string? ColorCode { get; set; }

    /// <summary>
    /// Tasks belonging to this stage.
    /// </summary>
    public List<ProductionPlanTaskRequest> Tasks { get; set; } = new();
}