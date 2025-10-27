namespace RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
public class ExpertPlanDetailResponse
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? StandardPlanId { get; set; }
    public Guid? GroupId { get; set; }
    public decimal? TotalArea { get; set; }
    public DateTime BasePlantingDate { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public decimal EstimatedTotalPlanCost { get; set; }

    public ExpertPlanGroupDetailResponse? GroupDetails { get; set; }
    public List<ExpertPlanStageResponse> Stages { get; set; } = new();
}