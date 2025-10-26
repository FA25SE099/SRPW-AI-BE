using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;

public class ExpertPlanGroupDetailResponse
{
    public Guid Id { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public decimal? TotalArea { get; set; }
    public GroupStatus Status { get; set; }
    public List<ExpertPlotResponse> Plots { get; set; } = new();
}