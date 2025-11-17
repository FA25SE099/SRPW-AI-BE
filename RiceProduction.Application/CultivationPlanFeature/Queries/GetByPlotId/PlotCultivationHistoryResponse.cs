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
    
    /// <summary>
    /// Năng suất thực tế (nếu đã thu hoạch).
    /// </summary>
    public decimal? ActualYield { get; set; }
    
    /// <summary>
    /// Tên của Kế hoạch Sản xuất (Production Plan) được liên kết với chu kỳ này.
    /// </summary>
    public string? ProductionPlanName { get; set; }
}