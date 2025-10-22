using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.Common.Models.Response;
public class ProductionPlanTaskResponse
{
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType TaskType { get; set; }
    
    /// <summary>
    /// Calculated start date based on BasePlantingDate and DaysAfter.
    /// </summary>
    public DateTime ScheduledDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public TaskPriority Priority { get; set; }
    public int SequenceOrder { get; set; }

    /// <summary>
    /// Calculated field: Sum of EstimatedAmount of all materials.
    /// </summary>
    public decimal EstimatedMaterialCost { get; set; }
    
    public List<ProductionPlanTaskMaterialResponse> Materials { get; set; } = new();
}