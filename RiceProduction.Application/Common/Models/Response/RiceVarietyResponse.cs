using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response;
public class RiceVarietyResponse
{
    public Guid Id { get; set; }
    public string VarietyName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? BaseGrowthDurationDays { get; set; }
    public decimal? BaseYieldPerHectare { get; set; }
    public string? Description { get; set; }
    public string? Characteristics { get; set; }
    public bool IsActive { get; set; }
    public List<RiceVarietySeasonInfo> AssociatedSeasons { get; set; } = new List<RiceVarietySeasonInfo>();
}

public class RiceVarietySeasonInfo
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int GrowthDurationDays { get; set; }
    public decimal? ExpectedYieldPerHectare { get; set; }
    public string OptimalPlantingStart { get; set; } = string.Empty;
    public string? OptimalPlantingEnd { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public bool IsRecommended { get; set; }
}