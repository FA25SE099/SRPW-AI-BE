namespace RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
public class ExpertPendingPlanItemResponse
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public string GroupArea { get; set; } = string.Empty; // Hiển thị diện tích của Group
    public DateTime BasePlantingDate { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmitterName { get; set; }
}