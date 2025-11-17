public class FarmerPlanViewResponse
{
    public Guid PlotCultivationId { get; set; }
    public Guid ProductionPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime BasePlantingDate { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus PlanStatus { get; set; }
    public decimal PlotArea { get; set; }

    public List<FarmerPlanStageViewResponse> Stages { get; set; } = new();
}