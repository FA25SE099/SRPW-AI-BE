namespace RiceProduction.Application.Common.Models.Response.GroupResponse;
public class GroupProductionPlanResponse
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime BasePlantingDate { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public decimal? TotalArea { get; set; }
}