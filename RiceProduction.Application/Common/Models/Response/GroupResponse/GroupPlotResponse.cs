using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.Common.Models.Response.GroupResponse;

public class GroupPlotResponse
{
    public Guid Id { get; set; }
    public decimal Area { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public string? SoilType { get; set; }
    public PlotStatus Status { get; set; }
    public string FarmerName { get; set; } = string.Empty;
}