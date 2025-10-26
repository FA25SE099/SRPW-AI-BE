using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.Common.Models.Response.GroupResponse;

public class GroupDetailResponse
{
    public Guid Id { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public Guid? SeasonId { get; set; }
    public DateTime? PlantingDate { get; set; }
    public GroupStatus Status { get; set; }
    public decimal? TotalArea { get; set; }
    public string? RiceVarietyName { get; set; }
    public string? SupervisorName { get; set; }

    public ICollection<GroupPlotResponse> Plots { get; set; } = new List<GroupPlotResponse>();
    public ICollection<GroupProductionPlanResponse> ProductionPlans { get; set; } = new List<GroupProductionPlanResponse>();
    public ICollection<GroupUavOrderResponse> UavServiceOrders { get; set; } = new List<GroupUavOrderResponse>();
    public ICollection<GroupAlertResponse> Alerts { get; set; } = new List<GroupAlertResponse>();
}