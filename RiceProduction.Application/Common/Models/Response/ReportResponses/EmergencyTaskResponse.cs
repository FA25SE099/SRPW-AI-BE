using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.Common.Models.Response.ReportResponses;

/// <summary>
/// Emergency task with calculated material costs
/// </summary>
public class EmergencyTaskResponse
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType? TaskType { get; set; }
    public TaskStatus? Status { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public bool IsContingency { get; set; }
    public string? ContingencyReason { get; set; }
    public decimal TotalTaskMaterialCost { get; set; }
    public List<EmergencyTaskMaterialResponse> Materials { get; set; } = new();
}
