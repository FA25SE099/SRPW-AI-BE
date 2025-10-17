using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;

public class ExpertPlanTaskResponse
{
    public Guid Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType TaskType { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public TaskPriority Priority { get; set; }
    public int SequenceOrder { get; set; }
    public decimal EstimatedMaterialCost { get; set; } // Tổng chi phí vật tư cho Task này
    public List<ExpertPlanTaskMaterialResponse> Materials { get; set; } = new();
}