using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetByPlotId;
public class PlotCultivationHistoryResponse
{
    public Guid PlotCultivationId { get; set; }
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    
    public DateTime PlantingDate { get; set; }
    public decimal? Area { get; set; }
    public CultivationStatus Status { get; set; }
    public decimal? ActualYield { get; set; }
    public string? ProductionPlanName { get; set; }
    public string? ActiveVersionName { get; set; }
}