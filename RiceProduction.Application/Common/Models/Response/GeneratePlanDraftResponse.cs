namespace RiceProduction.Application.Common.Models.Response;
public class GeneratePlanDraftResponse
{
    public Guid StandardPlanId { get; set; }
    public Guid GroupId { get; set; }

    /// <summary>
    /// Suggested Plan Name derived from Standard Plan name.
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// The actual area used for calculation, retrieved from the Group.
    /// </summary>
    public decimal TotalArea { get; set; }

    public DateTime BasePlantingDate { get; set; }
    public decimal EstimatedTotalPlanCost { get; set; }

    public List<ProductionStageResponse> Stages { get; set; } = new();
}