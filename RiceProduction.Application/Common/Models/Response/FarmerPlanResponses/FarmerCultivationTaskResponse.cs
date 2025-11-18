using RiceProduction.Domain.Enums;

public class FarmerCultivationTaskResponse
{
    public Guid Id { get; set; } // ID của CultivationTask
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType TaskType { get; set; }
    public DateTime ScheduledDate { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public bool IsContingency { get; set; }
    public decimal ActualMaterialCost { get; set; }
    public List<FarmerMaterialComparisonResponse> Materials { get; set; } = new();
}